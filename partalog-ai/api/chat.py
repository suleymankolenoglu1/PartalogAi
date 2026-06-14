"""
Partalog AI - Chat API (Final v4.2 - Turkish Native Mode + Hybrid Search 🇹🇷)
------------------------------------------------
1. NO DICTIONARY: Sözlük iptal. "SCREW" yok, "VİDA" var.
2. HYBRID SEARCH: Parça kodu varsa ÖNCE tam eşleşme, yoksa Vektör aranır.
3. SMART ROUTER: Marka, Parça ismi, KOD ve ÖLÇÜ(Dimensions) ayıklar.
4. MULTI-PART: Birden fazla parça istenirse "parts" listesi döndürür.
"""

import aiohttp
import json
from pathlib import Path

from dotenv import load_dotenv
from fastapi import APIRouter, File, Form, HTTPException, Request, Response, UploadFile
from fastapi.responses import StreamingResponse
from loguru import logger

from api.stream_contract import (
    build_done_event,
    build_sources_event,
    build_token_event,
    serialize_sse_event,
)
from config import settings
from core.rate_limiter import limiter
from services.ai_capacity import AiCapacityLimitExceeded, chat_capacity_guard
from services.chat_context import prepare_chat_context
from services.chat_feedback import process_visual_feedback
from services.chat_intent import ensure_unavailable_feature_notice as _ensure_unavailable_feature_notice
from services.chat_responses import (
    build_deterministic_reply_from_sources,
    sanitize_reply_safety_language,
)
from services.genai_provider import provider

# Ensure local .env is loaded for local dev fallbacks
_BASE_DIR = Path(__file__).resolve().parents[1]
_ENV_PATH = _BASE_DIR / ".env"
load_dotenv(_ENV_PATH)

CHAT_RATE_LIMIT = "60/minute"
VISUAL_FEEDBACK_RATE_LIMIT = "5/minute"
GEMINI_CHAT_GENERATION_CONFIG = {
    "temperature": 0.5,
    "maxOutputTokens": 650,
}

router = APIRouter()


def optional_chat_rate_limit(func):
    if settings.CHAT_HTTP_RATE_LIMIT_ENABLED:
        return limiter.limit(CHAT_RATE_LIMIT)(func)
    return func


def _plan_limit_message(
    user_plan: str | None,
    ai_limit_per_month: int | None,
    ai_used_this_month: int | None,
) -> str | None:
    if settings.DEBUG and settings.DEV_AI_QUOTA_BYPASS:
        logger.warning("DEV_AI_QUOTA_BYPASS açık: AI plan/kota kontrolü atlandı.")
        return None

    plan_text = (user_plan or "").strip().lower()
    if plan_text in {"catalogonly", "catalog", "1"}:
        return "AI sorgu limitinize ulaştınız, planınızı yükseltin"

    if ai_limit_per_month is not None and ai_limit_per_month >= 0:
        used = ai_used_this_month or 0
        if used >= ai_limit_per_month:
            return "AI sorgu limitinize ulaştınız, planınızı yükseltin"

    return None


@router.post("/send")
@router.post("/expert-chat")
@optional_chat_rate_limit
async def chat_endpoint(
    request: Request,
    response: Response,
    text: str = Form(None),
    message: str = Form(None),
    history: str = Form("[]"),
    catalog_ids: str = Form("[]"),
    context_json: str = Form("{}"),
    file: UploadFile = File(None),
    user_plan: str = Form(None),
    ai_limit_per_month: int = Form(None),
    ai_used_this_month: int = Form(None),
    policy_threshold_override: str = Form(None),
):
    capacity_lease = None
    try:
        limit_msg = _plan_limit_message(user_plan, ai_limit_per_month, ai_used_this_month)
        if limit_msg:
            return {
                "answer": limit_msg,
                "reply": limit_msg,
                "sources": [],
                "debug_intent": None,
            }

        capacity_lease = await chat_capacity_guard.try_acquire()

        ctx = await prepare_chat_context(
            text,
            message,
            history,
            catalog_ids,
            file,
            context_json,
            policy_threshold_override,
        )
        if ctx["early"]:
            return ctx["response"]

        api_url = provider.generate_content_url(settings.GEMINI_CHAT_MODEL)
        headers = await provider.build_headers()
        timeout = aiohttp.ClientTimeout(total=settings.GEMINI_CHAT_TIMEOUT_SECONDS)
        async with aiohttp.ClientSession(headers=headers, timeout=timeout) as session:
            payload = provider.normalize_generate_payload(
                {
                    "contents": [{"parts": [{"text": ctx["final_prompt"]}]}],
                    "generationConfig": GEMINI_CHAT_GENERATION_CONFIG,
                }
            )
            if not provider.has_credentials() or not api_url:
                logger.error("⚠️ GenAI credentials/config eksik.")
                fallback = build_deterministic_reply_from_sources(ctx["user_query"], ctx.get("all_sources", []))
                return {
                "answer": fallback,
                "reply": fallback,
                "sources": ctx.get("all_sources", []),
                "debug_intent": ctx.get("analysis"),
                "search_trace": ctx.get("search_trace"),
            }
            async with session.post(api_url, json=payload) as resp:
                if resp.status == 200:
                    ai_reply = (await resp.json())["candidates"][0]["content"]["parts"][0]["text"]
                else:
                    logger.warning(f"Gemini generateContent non-200: {resp.status}")
                    ai_reply = build_deterministic_reply_from_sources(ctx["user_query"], ctx.get("all_sources", []))
        ai_reply = sanitize_reply_safety_language(_ensure_unavailable_feature_notice(ai_reply, ctx.get("analysis")))

        return {
            "answer": ai_reply,
            "reply": ai_reply,
            "sources": ctx["all_sources"],
            "debug_intent": ctx["analysis"],
            "search_trace": ctx.get("search_trace"),
        }

    except AiCapacityLimitExceeded:
        raise HTTPException(
            status_code=429,
            detail={
                "message": settings.AI_CHAT_BUSY_MESSAGE,
                "reason": "ai_capacity_limited",
            },
        )
    except Exception as e:
        logger.error(f"Chat Hatası: {e}")
        return {
            "answer": "Sistemsel bir hata oluştu ustam.",
            "reply": "Hata",
            "sources": [],
            "debug_intent": None,
        }
    finally:
        if capacity_lease is not None:
            await capacity_lease.release()


@router.post("/stream")
async def chat_stream_endpoint(
    text: str = Form(None),
    message: str = Form(None),
    history: str = Form("[]"),
    catalog_ids: str = Form("[]"),
    context_json: str = Form("{}"),
    file: UploadFile = File(None),
    user_plan: str = Form(None),
    ai_limit_per_month: int = Form(None),
    ai_used_this_month: int = Form(None),
    policy_threshold_override: str = Form(None),
):
    async def event_generator():
        final_fallback_used = False
        final_fallback_reason = None
        capacity_lease = None

        try:
            limit_msg = _plan_limit_message(user_plan, ai_limit_per_month, ai_used_this_month)
            if limit_msg:
                yield serialize_sse_event(
                    build_sources_event([], fallback_used=True, fallback_reason="plan_limit")
                )
                yield serialize_sse_event(
                    build_token_event(
                        limit_msg,
                        fallback_used=True,
                        fallback_reason="plan_limit",
                    )
                )
                yield serialize_sse_event(
                    build_done_event(
                        fallback_used=True,
                        fallback_reason="plan_limit",
                    )
                )
                return

            capacity_lease = await chat_capacity_guard.try_acquire()

            ctx = await prepare_chat_context(
                text,
                message,
                history,
                catalog_ids,
                file,
                context_json,
                policy_threshold_override,
            )
            if ctx["early"]:
                resp = ctx["response"]
                yield serialize_sse_event(
                    build_sources_event(
                        resp.get("sources", []),
                        debug_intent=resp.get("debug_intent"),
                        search_trace=resp.get("search_trace"),
                        fallback_used=True,
                        fallback_reason="early_response",
                    )
                )
                yield serialize_sse_event(
                    build_token_event(
                        resp.get("answer", ""),
                        fallback_used=True,
                        fallback_reason="early_response",
                    )
                )
                yield serialize_sse_event(
                    build_done_event(
                        fallback_used=True,
                        fallback_reason="early_response",
                    )
                )
                return

            yield serialize_sse_event(
                build_sources_event(
                    ctx["all_sources"],
                    debug_intent=ctx["analysis"],
                    search_trace=ctx.get("search_trace"),
                )
            )

            payload = provider.normalize_generate_payload(
                {
                    "contents": [{"parts": [{"text": ctx["final_prompt"]}]}],
                    "generationConfig": GEMINI_CHAT_GENERATION_CONFIG,
                }
            )
            stream_url = provider.generate_content_url(settings.GEMINI_STREAM_MODEL, stream=True)
            headers = await provider.build_headers()
            timeout = aiohttp.ClientTimeout(
                total=settings.GEMINI_STREAM_TIMEOUT_SECONDS,
                sock_read=settings.GEMINI_STREAM_SOCK_READ_TIMEOUT_SECONDS,
            )
            async with aiohttp.ClientSession(headers=headers, timeout=timeout) as session:
                if not provider.has_credentials() or not stream_url:
                    logger.error("⚠️ GenAI credentials/config eksik.")
                    fallback = build_deterministic_reply_from_sources(ctx["user_query"], ctx.get("all_sources", []))
                    final_fallback_used = True
                    final_fallback_reason = "missing_api_key"
                    yield serialize_sse_event(
                        build_token_event(
                            fallback,
                            fallback_used=True,
                            fallback_reason=final_fallback_reason,
                        )
                    )
                    yield serialize_sse_event(
                        build_done_event(
                            fallback_used=True,
                            fallback_reason=final_fallback_reason,
                        )
                    )
                    return
                async with session.post(stream_url, json=payload) as resp:
                    logger.info(f"🤖 [GEMINI-STREAM] status={resp.status} content-type={resp.headers.get('Content-Type')}")
                    if resp.status != 200:
                        try:
                            err_text = await resp.text()
                        except Exception:
                            err_text = "<read-failed>"
                        logger.error(f"🤖 [GEMINI-STREAM] error body: {err_text[:500]}")
                        fallback = build_deterministic_reply_from_sources(ctx["user_query"], ctx.get("all_sources", []))
                        final_fallback_used = True
                        final_fallback_reason = "upstream_non_200"
                        yield serialize_sse_event(
                            build_token_event(
                                fallback,
                                fallback_used=True,
                                fallback_reason=final_fallback_reason,
                            )
                        )
                        yield serialize_sse_event(
                            build_done_event(
                                fallback_used=True,
                                fallback_reason=final_fallback_reason,
                            )
                        )
                        return
                    buffer = ""
                    done = False
                    token_count = 0
                    line_count = 0
                    streamed_reply = ""
                    async for chunk in resp.content.iter_any():
                        buffer += chunk.decode("utf-8")
                        while "\n" in buffer:
                            line, buffer = buffer.split("\n", 1)
                            line = line.strip()
                            if not line:
                                continue
                            if not line.startswith("data:"):
                                continue
                            raw = line[5:].strip()
                            if raw == "[DONE]":
                                done = True
                                break
                            line_count += 1
                            if line_count <= 3:
                                logger.debug(f"🤖 [GEMINI-STREAM] raw line sample: {raw[:200]}")
                            try:
                                chunk_json = json.loads(raw)
                                parts = (
                                    chunk_json.get("candidates", [{}])[0]
                                    .get("content", {})
                                    .get("parts", [])
                                )
                                token = parts[0].get("text") if parts else None
                                if token and token.strip():
                                    token_count += 1
                                    streamed_reply += token
                                    yield serialize_sse_event(build_token_event(token))
                            except Exception as parse_err:
                                logger.debug(f"SSE chunk parse atlandı: {parse_err} | raw={raw[:80]}")
                                continue
                        if done:
                            break
                    if token_count == 0:
                        logger.warning("🤖 [GEMINI-STREAM] 0 token üretildi. Yanıt formatı beklenenden farklı olabilir.")
                        fallback = build_deterministic_reply_from_sources(ctx["user_query"], ctx.get("all_sources", []))
                        final_fallback_used = True
                        final_fallback_reason = "zero_tokens"
                        yield serialize_sse_event(
                            build_token_event(
                                fallback,
                                fallback_used=True,
                                fallback_reason=final_fallback_reason,
                            )
                        )
                        streamed_reply = fallback
                    guarded_reply = sanitize_reply_safety_language(
                        _ensure_unavailable_feature_notice(streamed_reply, ctx.get("analysis"))
                    )
                    if guarded_reply != streamed_reply:
                        suffix = guarded_reply[len(streamed_reply):]
                        if suffix:
                            yield serialize_sse_event(build_token_event(suffix))

        except AiCapacityLimitExceeded:
            final_fallback_used = True
            final_fallback_reason = "ai_capacity_limited"
            yield serialize_sse_event(
                build_sources_event(
                    [],
                    fallback_used=True,
                    fallback_reason=final_fallback_reason,
                )
            )
            yield serialize_sse_event(
                build_token_event(
                    settings.AI_CHAT_BUSY_MESSAGE,
                    fallback_used=True,
                    fallback_reason=final_fallback_reason,
                )
            )
        except Exception as e:
            logger.error(f"Chat Stream Hatası: {e}")
            final_fallback_used = True
            final_fallback_reason = "exception"
            yield serialize_sse_event(
                build_token_event(
                    "Sistemsel bir hata oluştu ustam.",
                    fallback_used=True,
                    fallback_reason=final_fallback_reason,
                )
            )
        finally:
            if capacity_lease is not None:
                await capacity_lease.release()

        yield serialize_sse_event(
            build_done_event(
                fallback_used=final_fallback_used,
                fallback_reason=final_fallback_reason,
            )
        )

    return StreamingResponse(event_generator(), media_type="text/event-stream")


@router.post("/visual-feedback")
@limiter.limit(VISUAL_FEEDBACK_RATE_LIMIT)
async def visual_feedback_endpoint(
    request: Request,
    file: UploadFile = File(...),
    part_name: str = Form(None),
    part_code: str = Form(None),
    machine_brand: str = Form(None),
    machine_type: str = Form(None),
    user_id: str = Form(None),
    note: str = Form(None),
):
    try:
        if file is None:
            return {"success": False, "message": "Fotoğraf zorunlu."}

        image_bytes = await file.read()
        if not image_bytes:
            return {"success": False, "message": "Boş dosya."}

        if not part_name and not part_code:
            return {"success": False, "message": "part_name veya part_code zorunlu."}

        return await process_visual_feedback(
            file_bytes=image_bytes,
            original_filename=file.filename,
            part_name=part_name,
            part_code=part_code,
            machine_brand=machine_brand,
            machine_type=machine_type,
            user_id=user_id,
            note=note,
        )
    except Exception as e:
        logger.error(f"visual-feedback error: {e}")
        return {"success": False, "message": "Geri bildirim kaydedilemedi."}
