"""
Hotspot OCR - Balloon içindeki numaraları okur
EasyOCR + OpenCV ön işleme + Akıllı Filtreleme v3
"""

import re
from typing import List, Optional, Tuple

import cv2
import easyocr
import numpy as np
from loguru import logger


class HotspotOCR:
    """
    Hotspot/balloon içindeki rakamları okur.
    Siyah daire içindeki beyaz numaralar için optimize edilmiş.
    """

    def __init__(self, use_gpu: bool = False):
        self.use_gpu = use_gpu

        logger.info("OCR Reader başlatılıyor (gpu={})".format(self.use_gpu))

        self.reader = easyocr.Reader([
            'en'
        ], gpu=self.use_gpu, verbose=False)

        logger.info("OCR Reader hazır")

    def read_number(self, image: np.ndarray) -> Optional[str]:
        """Hotspot görüntüsünden en iyi rakam tahminini döndür."""
        result, _ = self.read_number_with_confidence(image)
        return result

    def read_number_with_confidence(self, image: np.ndarray) -> Tuple[Optional[str], float]:
        """Hotspot crop'undan label ve yaklaşık confidence döndür."""
        if image is None or image.size == 0:
            return None, 0.0

        image = self._normalize_input(image)
        candidates: List[Tuple[str, float, str]] = []

        methods = [
            ("tight_dark", lambda img: self._preprocess_dark_digits(self._crop_center(img, 0.5))),
            ("tight_blackhat", lambda img: self._preprocess_blackhat_digits(self._crop_center(img, 0.52))),
            ("center_auto", lambda img: self._preprocess_auto_polarity(self._crop_center(img, 0.6))),
            ("center_clahe", lambda img: self._preprocess_clahe_binary(self._crop_center(img, 0.62))),
            ("center_inverted", lambda img: self._preprocess_inverted(self._crop_center(img, 0.6))),
            ("center_adaptive", lambda img: self._preprocess_adaptive(self._crop_center(img, 0.6))),
            ("full_auto", self._preprocess_auto_polarity),
            ("full_inverted", self._preprocess_inverted),
            ("mask_circle", self._preprocess_with_circle_mask),
        ]

        for method_name, method in methods:
            try:
                processed = method(image)
                result, confidence = self._ocr_read_with_confidence(processed)
                if result:
                    candidates.append((result, confidence, method_name))
            except Exception as exc:
                logger.debug("OCR method '{}' failed: {}".format(method_name, str(exc)))
                continue

        if not candidates:
            return None, 0.0

        best_result = self._select_best_result(candidates)
        if not best_result:
            return None, 0.0

        confidences = [confidence for result, confidence, _ in candidates if result == best_result]
        avg_conf = sum(confidences) / len(confidences)
        boosted_conf = avg_conf + (0.08 if len(confidences) > 1 else 0.0)
        if len(best_result) <= 2:
            boosted_conf += 0.04
        else:
            boosted_conf -= 0.08

        logger.debug(
            "OCR final '{}' (candidates: {})".format(
                best_result,
                [(c[0], round(c[1], 2), c[2]) for c in candidates]
            )
        )

        return best_result, float(min(0.99, max(0.0, boosted_conf)))

    def _crop_center(self, image: np.ndarray, ratio: float = 0.7) -> np.ndarray:
        """Görüntünün merkez bölgesini kırp."""
        h, w = image.shape[:2]
        margin_x = max(int(w * (1 - ratio) / 2), 3)
        margin_y = max(int(h * (1 - ratio) / 2), 3)
        cropped = image[margin_y:h - margin_y, margin_x:w - margin_x]
        if cropped.shape[0] < 10 or cropped.shape[1] < 10:
            return image
        return cropped

    def _preprocess_with_circle_mask(self, image: np.ndarray) -> np.ndarray:
        """Daire maskesi uygula - sadece balloon içini al."""
        h, w = image.shape[:2]
        gray = self._to_gray(image)

        mask = np.zeros((h, w), dtype=np.uint8)
        center = (w // 2, h // 2)
        radius = int(min(w, h) * 0.4)
        cv2.circle(mask, center, radius, 255, -1)

        masked = cv2.bitwise_and(gray, gray, mask=mask)
        masked[mask == 0] = 255

        inverted = cv2.bitwise_not(masked)
        _, binary = cv2.threshold(inverted, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
        return cv2.resize(binary, None, fx=3, fy=3, interpolation=cv2.INTER_CUBIC)

    def _ocr_read_with_confidence(self, image: np.ndarray) -> Tuple[Optional[str], float]:
        """OCR uygula ve confidence ile birlikte döndür."""
        results = self.reader.readtext(
            image,
            allowlist='0123456789',
            detail=1,
            paragraph=False,
            contrast_ths=0.05,
            adjust_contrast=0.75,
            text_threshold=0.35,
            low_text=0.2,
            link_threshold=0.2,
        )

        if not results:
            return None, 0.0

        best_result = max(results, key=lambda x: x[2])
        text = re.sub(r'[^0-9]', '', best_result[1].strip())
        confidence = float(best_result[2])

        if not text:
            return None, 0.0
        if confidence < 0.25 and len(text) > 2:
            return None, 0.0

        if len(text) <= 2:
            return text, confidence
        if len(text) == 3:
            return self._correct_3digit(text, confidence)
        return self._correct_4digit_plus(text, confidence)

    def _correct_3digit(self, text: str, confidence: float) -> Tuple[Optional[str], float]:
        """3 haneli sonuçları düzelt."""
        first_char = text[0]
        rest = text[1:]
        suspicious_first_chars = ['4', '5', '8', '1', '2', '3', '7']

        if first_char in suspicious_first_chars and rest.isdigit() and len(rest) == 2:
            return rest, confidence * 0.85

        if text[-1] == text[-2]:
            return text[:-1], confidence * 0.9

        if confidence < 0.6:
            return None, 0.0

        return text, confidence

    def _correct_4digit_plus(self, text: str, confidence: float) -> Tuple[Optional[str], float]:
        """4+ haneli sonuçları düzelt."""
        if len(text) == 4:
            candidate = text[1:]
            if candidate.isdigit():
                return candidate, confidence * 0.7

            candidate = text[2:]
            if candidate.isdigit() and len(candidate) >= 1:
                return candidate, confidence * 0.6

        if len(text) >= 5:
            mid = len(text) // 2
            candidate = text[mid - 1:mid + 1]
            if candidate.isdigit():
                return candidate, confidence * 0.5

        return None, 0.0

    def _select_best_result(self, candidates: List[Tuple[str, float, str]]) -> Optional[str]:
        """Birden fazla OCR sonucundan en iyisini seç."""
        if not candidates:
            return None

        if len(candidates) == 1:
            result, conf, _ = candidates[0]
            return result if conf >= 0.3 else None

        result_groups: dict[str, List[Tuple[float, str]]] = {}
        for result, conf, method in candidates:
            result_groups.setdefault(result, []).append((conf, method))

        best_result = None
        best_score = 0.0
        for result, occurrences in result_groups.items():
            count = len(occurrences)
            avg_conf = sum(conf for conf, _ in occurrences) / count
            method_bonus = max(self._method_priority(method) for _, method in occurrences)
            length_penalty = 1.15 if len(result) <= 2 else 0.55 if len(result) == 3 else 0.25
            score = count * avg_conf * method_bonus * length_penalty
            if score > best_score:
                best_score = score
                best_result = result

        if best_score < 0.5:
            candidates.sort(key=lambda x: (len(x[0]), -x[1]))
            result, conf, _ = candidates[0]
            if conf >= 0.4 and len(result) <= 2:
                return result
            return None

        return best_result

    def _preprocess_inverted(self, image: np.ndarray) -> np.ndarray:
        """Siyah zemin beyaz yazı için ters çevir."""
        gray = self._to_gray(image)
        inverted = cv2.bitwise_not(gray)
        clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
        enhanced = clahe.apply(inverted)
        denoised = cv2.medianBlur(enhanced, 3)
        _, binary = cv2.threshold(denoised, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
        return cv2.resize(binary, None, fx=3, fy=3, interpolation=cv2.INTER_CUBIC)

    def _preprocess_dark_digits(self, image: np.ndarray) -> np.ndarray:
        """Siyah hotspot içindeki beyaz rakamları agresif izole et."""
        gray = self._to_gray(image)
        blur = cv2.GaussianBlur(gray, (3, 3), 0)
        clahe = cv2.createCLAHE(clipLimit=3.2, tileGridSize=(4, 4))
        enhanced = clahe.apply(blur)

        if self._is_dark_background(enhanced):
            _, binary = cv2.threshold(enhanced, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
        else:
            _, binary = cv2.threshold(enhanced, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)

        kernel = np.ones((2, 2), np.uint8)
        binary = cv2.morphologyEx(binary, cv2.MORPH_OPEN, kernel)
        binary = cv2.morphologyEx(binary, cv2.MORPH_CLOSE, kernel)
        focused = self._extract_main_component(binary)
        return cv2.resize(focused, None, fx=3.2, fy=3.2, interpolation=cv2.INTER_CUBIC)

    def _preprocess_blackhat_digits(self, image: np.ndarray) -> np.ndarray:
        """Blackhat/top-hat karışımıyla merkez rakamları öne çıkar."""
        gray = self._to_gray(image)
        gray = cv2.GaussianBlur(gray, (5, 5), 0)
        normalized = cv2.bitwise_not(gray) if self._is_dark_background(gray) else gray

        kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5))
        emphasized = cv2.morphologyEx(normalized, cv2.MORPH_TOPHAT, kernel)
        emphasized = cv2.normalize(emphasized, None, 0, 255, cv2.NORM_MINMAX)
        _, binary = cv2.threshold(emphasized, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
        focused = self._extract_main_component(binary)
        return cv2.resize(focused, None, fx=3.4, fy=3.4, interpolation=cv2.INTER_CUBIC)

    def _preprocess_adaptive(self, image: np.ndarray) -> np.ndarray:
        """Adaptive threshold ile ön işleme."""
        gray = self._to_gray(image)
        binary = cv2.adaptiveThreshold(
            gray, 255, cv2.ADAPTIVE_THRESH_GAUSSIAN_C, cv2.THRESH_BINARY_INV, 11, 2
        )
        kernel = np.ones((2, 2), np.uint8)
        binary = cv2.morphologyEx(binary, cv2.MORPH_CLOSE, kernel)
        return cv2.resize(binary, None, fx=3, fy=3, interpolation=cv2.INTER_CUBIC)

    def read_numbers_batch(self, images: List[np.ndarray]) -> List[Optional[str]]:
        """Birden fazla görüntüden numara oku."""
        return [self.read_number(img) for img in images]

    def _normalize_input(self, image: np.ndarray) -> np.ndarray:
        """Çok küçük crop'larda OCR'ın çökmesini önlemek için minimum boyut uygula."""
        h, w = image.shape[:2]
        if h >= 40 and w >= 40:
            return image

        min_side = 48
        scale = max(min_side / max(h, 1), min_side / max(w, 1))
        scale = min(4.0, max(1.0, scale))
        return cv2.resize(image, None, fx=scale, fy=scale, interpolation=cv2.INTER_CUBIC)

    def _to_gray(self, image: np.ndarray) -> np.ndarray:
        if len(image.shape) == 3:
            return cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        return image.copy()

    def _is_dark_background(self, gray: np.ndarray) -> bool:
        """Merkez bölgenin karanlık oranına göre polarite tahmini yap."""
        h, w = gray.shape[:2]
        cy1, cy2 = int(h * 0.2), int(h * 0.8)
        cx1, cx2 = int(w * 0.2), int(w * 0.8)
        roi = gray[cy1:cy2, cx1:cx2]
        if roi.size == 0:
            roi = gray
        dark_ratio = float((roi < 95).sum()) / float(roi.size)
        return dark_ratio > 0.5

    def _preprocess_auto_polarity(self, image: np.ndarray) -> np.ndarray:
        """Zemin koyu/açık durumuna göre otomatik threshold polaritesi seç."""
        gray = self._to_gray(image)
        gray = cv2.GaussianBlur(gray, (3, 3), 0)
        clahe = cv2.createCLAHE(clipLimit=2.2, tileGridSize=(8, 8))
        enhanced = clahe.apply(gray)

        if self._is_dark_background(enhanced):
            _, binary = cv2.threshold(enhanced, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
        else:
            _, binary = cv2.threshold(enhanced, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)

        kernel = np.ones((2, 2), np.uint8)
        binary = cv2.morphologyEx(binary, cv2.MORPH_CLOSE, kernel)
        return cv2.resize(binary, None, fx=3, fy=3, interpolation=cv2.INTER_CUBIC)

    def _preprocess_clahe_binary(self, image: np.ndarray) -> np.ndarray:
        """Siyah hotspot üstündeki beyaz rakamlarda kontrastı güçlendir."""
        gray = self._to_gray(image)
        clahe = cv2.createCLAHE(clipLimit=2.8, tileGridSize=(6, 6))
        enhanced = clahe.apply(gray)
        sharpen = cv2.addWeighted(enhanced, 1.45, cv2.GaussianBlur(enhanced, (0, 0), 1.2), -0.45, 0)

        if self._is_dark_background(sharpen):
            binary = cv2.adaptiveThreshold(
                sharpen, 255, cv2.ADAPTIVE_THRESH_GAUSSIAN_C, cv2.THRESH_BINARY, 13, 3
            )
        else:
            binary = cv2.adaptiveThreshold(
                sharpen, 255, cv2.ADAPTIVE_THRESH_GAUSSIAN_C, cv2.THRESH_BINARY_INV, 13, 3
            )

        kernel = np.ones((2, 2), np.uint8)
        binary = cv2.morphologyEx(binary, cv2.MORPH_OPEN, kernel)
        return cv2.resize(binary, None, fx=3, fy=3, interpolation=cv2.INTER_CUBIC)

    def _extract_main_component(self, binary: np.ndarray) -> np.ndarray:
        """Merkezdeki baskın bileşeni bırakıp kenar gürültüsünü at."""
        if binary is None or binary.size == 0:
            return binary

        num_labels, labels, stats, centroids = cv2.connectedComponentsWithStats(binary, connectivity=8)
        if num_labels <= 1:
            return binary

        h, w = binary.shape[:2]
        center = np.array([w / 2.0, h / 2.0])
        best_label = None
        best_score = 0.0

        for label in range(1, num_labels):
            _, _, width, height, area = stats[label]
            if area < max(6, int((h * w) * 0.003)):
                continue

            cx, cy = centroids[label]
            distance = np.linalg.norm(np.array([cx, cy]) - center)
            distance_score = 1.0 / (1.0 + distance)
            aspect_score = min(width, height) / max(max(width, height), 1)
            score = float(area) * distance_score * (1.0 + aspect_score)
            if score > best_score:
                best_score = score
                best_label = label

        if best_label is None:
            return binary

        focused = np.zeros_like(binary)
        focused[labels == best_label] = 255
        kernel = np.ones((2, 2), np.uint8)
        return cv2.morphologyEx(focused, cv2.MORPH_CLOSE, kernel)

    def _method_priority(self, method_name: str) -> float:
        priorities = {
            'tight_dark': 1.2,
            'tight_blackhat': 1.15,
            'center_clahe': 1.08,
            'center_auto': 1.05,
            'center_inverted': 1.0,
            'center_adaptive': 0.98,
            'mask_circle': 0.95,
            'full_auto': 0.9,
            'full_inverted': 0.85,
        }
        return priorities.get(method_name, 0.9)

    def get_info(self) -> dict:
        """OCR bilgilerini döndür."""
        return {
            'engine': 'EasyOCR',
            'gpu_enabled': self.use_gpu,
            'allowed_chars': '0123456789',
            'features': [
                'center_crop',
                'tight_dark_digits',
                'blackhat_focus',
                'circle_mask',
                'auto_polarity',
                'clahe_enhancement',
                'voting',
                '3digit_correction',
                '4digit_correction',
            ]
        }


OCRReader = HotspotOCR
