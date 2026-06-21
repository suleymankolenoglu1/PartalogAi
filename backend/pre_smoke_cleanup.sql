--
-- PostgreSQL database dump
--

\restrict z4dHnhboAyzSORpbu7wX19THTm47qFRD7cUFBxnIKfhZdyTQmomSdvp1Fae6fr4

-- Dumped from database version 16.13 (Debian 16.13-1.pgdg12+1)
-- Dumped by pg_dump version 16.13 (Debian 16.13-1.pgdg12+1)

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: hangfire; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA hangfire;


ALTER SCHEMA hangfire OWNER TO postgres;

--
-- Name: vector; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS vector WITH SCHEMA public;


--
-- Name: EXTENSION vector; Type: COMMENT; Schema: -; Owner:
--

COMMENT ON EXTENSION vector IS 'vector data type and ivfflat and hnsw access methods';


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: aggregatedcounter; Type: TABLE; Schema: hangfire; Owner: postgres
--

CREATE TABLE hangfire.aggregatedcounter (
    id bigint NOT NULL,
    key text NOT NULL,
    value bigint NOT NULL,
    expireat timestamp with time zone
);


ALTER TABLE hangfire.aggregatedcounter OWNER TO postgres;

--
-- Name: aggregatedcounter_id_seq; Type: SEQUENCE; Schema: hangfire; Owner: postgres
--

CREATE SEQUENCE hangfire.aggregatedcounter_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE hangfire.aggregatedcounter_id_seq OWNER TO postgres;

--
-- Name: aggregatedcounter_id_seq; Type: SEQUENCE OWNED BY; Schema: hangfire; Owner: postgres
--

ALTER SEQUENCE hangfire.aggregatedcounter_id_seq OWNED BY hangfire.aggregatedcounter.id;


--
-- Name: counter; Type: TABLE; Schema: hangfire; Owner: postgres
--

CREATE TABLE hangfire.counter (
    id bigint NOT NULL,
    key text NOT NULL,
    value bigint NOT NULL,
    expireat timestamp with time zone
);


ALTER TABLE hangfire.counter OWNER TO postgres;

--
-- Name: counter_id_seq; Type: SEQUENCE; Schema: hangfire; Owner: postgres
--

CREATE SEQUENCE hangfire.counter_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE hangfire.counter_id_seq OWNER TO postgres;

--
-- Name: counter_id_seq; Type: SEQUENCE OWNED BY; Schema: hangfire; Owner: postgres
--

ALTER SEQUENCE hangfire.counter_id_seq OWNED BY hangfire.counter.id;


--
-- Name: hash; Type: TABLE; Schema: hangfire; Owner: postgres
--

CREATE TABLE hangfire.hash (
    id bigint NOT NULL,
    key text NOT NULL,
    field text NOT NULL,
    value text,
    expireat timestamp with time zone,
    updatecount integer DEFAULT 0 NOT NULL
);


ALTER TABLE hangfire.hash OWNER TO postgres;

--
-- Name: hash_id_seq; Type: SEQUENCE; Schema: hangfire; Owner: postgres
--

CREATE SEQUENCE hangfire.hash_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE hangfire.hash_id_seq OWNER TO postgres;

--
-- Name: hash_id_seq; Type: SEQUENCE OWNED BY; Schema: hangfire; Owner: postgres
--

ALTER SEQUENCE hangfire.hash_id_seq OWNED BY hangfire.hash.id;


--
-- Name: job; Type: TABLE; Schema: hangfire; Owner: postgres
--

CREATE TABLE hangfire.job (
    id bigint NOT NULL,
    stateid bigint,
    statename text,
    invocationdata jsonb NOT NULL,
    arguments jsonb NOT NULL,
    createdat timestamp with time zone NOT NULL,
    expireat timestamp with time zone,
    updatecount integer DEFAULT 0 NOT NULL
);


ALTER TABLE hangfire.job OWNER TO postgres;

--
-- Name: job_id_seq; Type: SEQUENCE; Schema: hangfire; Owner: postgres
--

CREATE SEQUENCE hangfire.job_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE hangfire.job_id_seq OWNER TO postgres;

--
-- Name: job_id_seq; Type: SEQUENCE OWNED BY; Schema: hangfire; Owner: postgres
--

ALTER SEQUENCE hangfire.job_id_seq OWNED BY hangfire.job.id;


--
-- Name: jobparameter; Type: TABLE; Schema: hangfire; Owner: postgres
--

CREATE TABLE hangfire.jobparameter (
    id bigint NOT NULL,
    jobid bigint NOT NULL,
    name text NOT NULL,
    value text,
    updatecount integer DEFAULT 0 NOT NULL
);


ALTER TABLE hangfire.jobparameter OWNER TO postgres;

--
-- Name: jobparameter_id_seq; Type: SEQUENCE; Schema: hangfire; Owner: postgres
--

CREATE SEQUENCE hangfire.jobparameter_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE hangfire.jobparameter_id_seq OWNER TO postgres;

--
-- Name: jobparameter_id_seq; Type: SEQUENCE OWNED BY; Schema: hangfire; Owner: postgres
--

ALTER SEQUENCE hangfire.jobparameter_id_seq OWNED BY hangfire.jobparameter.id;


--
-- Name: jobqueue; Type: TABLE; Schema: hangfire; Owner: postgres
--

CREATE TABLE hangfire.jobqueue (
    id bigint NOT NULL,
    jobid bigint NOT NULL,
    queue text NOT NULL,
    fetchedat timestamp with time zone,
    updatecount integer DEFAULT 0 NOT NULL
);


ALTER TABLE hangfire.jobqueue OWNER TO postgres;

--
-- Name: jobqueue_id_seq; Type: SEQUENCE; Schema: hangfire; Owner: postgres
--

CREATE SEQUENCE hangfire.jobqueue_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE hangfire.jobqueue_id_seq OWNER TO postgres;

--
-- Name: jobqueue_id_seq; Type: SEQUENCE OWNED BY; Schema: hangfire; Owner: postgres
--

ALTER SEQUENCE hangfire.jobqueue_id_seq OWNED BY hangfire.jobqueue.id;


--
-- Name: list; Type: TABLE; Schema: hangfire; Owner: postgres
--

CREATE TABLE hangfire.list (
    id bigint NOT NULL,
    key text NOT NULL,
    value text,
    expireat timestamp with time zone,
    updatecount integer DEFAULT 0 NOT NULL
);


ALTER TABLE hangfire.list OWNER TO postgres;

--
-- Name: list_id_seq; Type: SEQUENCE; Schema: hangfire; Owner: postgres
--

CREATE SEQUENCE hangfire.list_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE hangfire.list_id_seq OWNER TO postgres;

--
-- Name: list_id_seq; Type: SEQUENCE OWNED BY; Schema: hangfire; Owner: postgres
--

ALTER SEQUENCE hangfire.list_id_seq OWNED BY hangfire.list.id;


--
-- Name: lock; Type: TABLE; Schema: hangfire; Owner: postgres
--

CREATE TABLE hangfire.lock (
    resource text NOT NULL,
    updatecount integer DEFAULT 0 NOT NULL,
    acquired timestamp with time zone
);


ALTER TABLE hangfire.lock OWNER TO postgres;

--
-- Name: schema; Type: TABLE; Schema: hangfire; Owner: postgres
--

CREATE TABLE hangfire.schema (
    version integer NOT NULL
);


ALTER TABLE hangfire.schema OWNER TO postgres;

--
-- Name: server; Type: TABLE; Schema: hangfire; Owner: postgres
--

CREATE TABLE hangfire.server (
    id text NOT NULL,
    data jsonb,
    lastheartbeat timestamp with time zone NOT NULL,
    updatecount integer DEFAULT 0 NOT NULL
);


ALTER TABLE hangfire.server OWNER TO postgres;

--
-- Name: set; Type: TABLE; Schema: hangfire; Owner: postgres
--

CREATE TABLE hangfire.set (
    id bigint NOT NULL,
    key text NOT NULL,
    score double precision NOT NULL,
    value text NOT NULL,
    expireat timestamp with time zone,
    updatecount integer DEFAULT 0 NOT NULL
);


ALTER TABLE hangfire.set OWNER TO postgres;

--
-- Name: set_id_seq; Type: SEQUENCE; Schema: hangfire; Owner: postgres
--

CREATE SEQUENCE hangfire.set_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE hangfire.set_id_seq OWNER TO postgres;

--
-- Name: set_id_seq; Type: SEQUENCE OWNED BY; Schema: hangfire; Owner: postgres
--

ALTER SEQUENCE hangfire.set_id_seq OWNED BY hangfire.set.id;


--
-- Name: state; Type: TABLE; Schema: hangfire; Owner: postgres
--

CREATE TABLE hangfire.state (
    id bigint NOT NULL,
    jobid bigint NOT NULL,
    name text NOT NULL,
    reason text,
    createdat timestamp with time zone NOT NULL,
    data jsonb,
    updatecount integer DEFAULT 0 NOT NULL
);


ALTER TABLE hangfire.state OWNER TO postgres;

--
-- Name: state_id_seq; Type: SEQUENCE; Schema: hangfire; Owner: postgres
--

CREATE SEQUENCE hangfire.state_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE hangfire.state_id_seq OWNER TO postgres;

--
-- Name: state_id_seq; Type: SEQUENCE OWNED BY; Schema: hangfire; Owner: postgres
--

ALTER SEQUENCE hangfire.state_id_seq OWNED BY hangfire.state.id;


--
-- Name: CatalogAiJobs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."CatalogAiJobs" (
    "Id" uuid NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone,
    "CatalogId" uuid NOT NULL,
    "Status" character varying(32) NOT NULL,
    "AttemptCount" integer DEFAULT 0 NOT NULL,
    "MaxAttempts" integer DEFAULT 3 NOT NULL,
    "NextAttemptAt" timestamp with time zone NOT NULL,
    "LastAttemptAt" timestamp with time zone,
    "LockedUntil" timestamp with time zone,
    "LastError" character varying(2048)
);


ALTER TABLE public."CatalogAiJobs" OWNER TO postgres;

--
-- Name: CatalogItemExternalMatches; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."CatalogItemExternalMatches" (
    "Id" uuid NOT NULL,
    "CatalogId" uuid NOT NULL,
    "CatalogPageId" uuid,
    "CatalogItemId" uuid NOT NULL,
    "ExternalSiteId" uuid NOT NULL,
    "ExternalProductId" uuid,
    "ExternalProductUrl" character varying(2048),
    "ExternalProductTitle" character varying(512),
    "ConfidenceScore" numeric(5,4) NOT NULL,
    "Status" character varying(32) NOT NULL,
    "MatchedBy" character varying(16) NOT NULL,
    "IsActive" boolean NOT NULL,
    "MatchedAtUtc" timestamp with time zone,
    "ReviewedByUserId" uuid,
    "ReviewedAtUtc" timestamp with time zone,
    "ReviewNote" character varying(1024),
    "MatchReasonsJson" text,
    "LastLinkCheckAtUtc" timestamp with time zone,
    "LastLinkStatusCode" integer,
    "IsLinkHealthy" boolean,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone
);


ALTER TABLE public."CatalogItemExternalMatches" OWNER TO postgres;

--
-- Name: CatalogItems; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."CatalogItems" (
    "Id" uuid NOT NULL,
    "CatalogId" uuid NOT NULL,
    "PageNumber" text NOT NULL,
    "RefNumber" text NOT NULL,
    "PartCode" text NOT NULL,
    "PartName" text NOT NULL,
    "Description" text NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone,
    "Embedding" public.vector(3072),
    "Dimensions" text,
    "MachineBrand" text,
    "MachineModel" text,
    "Mechanism" text,
    "MachineGroup" text,
    "VisualBbox" jsonb,
    "VisualEmbedding" public.vector(3072),
    "VisualOcrText" text,
    "VisualPageNumber" integer,
    "VisualShapeTags" jsonb,
    "VisualImageUrl" text
);


ALTER TABLE public."CatalogItems" OWNER TO postgres;

--
-- Name: CatalogPages; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."CatalogPages" (
    "Id" uuid NOT NULL,
    "PageNumber" integer NOT NULL,
    "ImageUrl" text NOT NULL,
    "CatalogId" uuid NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone,
    "AiDescription" text DEFAULT ''::text NOT NULL,
    "IsTechnicalDrawing" boolean,
    "ReviewedAt" timestamp with time zone,
    "ReviewNotes" character varying(1024),
    "ReviewStatus" character varying(32) DEFAULT 'NeedsReview'::character varying NOT NULL
);


ALTER TABLE public."CatalogPages" OWNER TO postgres;

--
-- Name: CatalogViews; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."CatalogViews" (
    "Id" uuid NOT NULL,
    "CatalogId" uuid NOT NULL,
    "OwnerUserId" uuid NOT NULL,
    "FingerprintHash" text NOT NULL,
    "BucketStartUtc" timestamp with time zone NOT NULL,
    "ViewedAtUtc" timestamp with time zone NOT NULL,
    "Source" text NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone
);


ALTER TABLE public."CatalogViews" OWNER TO postgres;

--
-- Name: Catalogs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."Catalogs" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" text NOT NULL,
    "ImageUrl" text NOT NULL,
    "PdfUrl" text NOT NULL,
    "Status" text NOT NULL,
    "UserId" uuid NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone,
    "FolderId" uuid
);


ALTER TABLE public."Catalogs" OWNER TO postgres;

--
-- Name: Customers; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."Customers" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "FullName" text NOT NULL,
    "Phone" text NOT NULL,
    "NormalizedPhone" text NOT NULL,
    "Email" text,
    "CompanyName" text,
    "Note" text,
    "LastVisitDate" timestamp with time zone NOT NULL,
    "LastOrderDate" timestamp with time zone,
    "OrderCount" integer NOT NULL,
    "TotalSpent" numeric(18,2) NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone,
    "LastLoginDate" timestamp with time zone,
    "LoginCode" text,
    "LoginCodeExpiresAt" timestamp with time zone,
    "PublicSessionExpiresAt" timestamp with time zone,
    "PublicSessionToken" text,
    "PasswordHash" text,
    "PasswordSalt" text,
    "FailedLoginCount" integer DEFAULT 0 NOT NULL,
    "LoginLockoutUntil" timestamp with time zone
);


ALTER TABLE public."Customers" OWNER TO postgres;

--
-- Name: EmbedSettings; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."EmbedSettings" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "AllowedOrigins" text DEFAULT '[]'::text NOT NULL,
    "Theme" text DEFAULT 'default'::text NOT NULL,
    "Mode" text DEFAULT 'catalog'::text NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone
);


ALTER TABLE public."EmbedSettings" OWNER TO postgres;

--
-- Name: EmbedTargets; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."EmbedTargets" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Name" character varying(160) NOT NULL,
    "Type" character varying(32) NOT NULL,
    "CatalogId" uuid NOT NULL,
    "CatalogPageId" uuid,
    "EmbedKey" character varying(96) NOT NULL,
    "CommerceMode" character varying(32) NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone,
    "ExistingCartMethod" character varying(16),
    "ExistingCartUrl" character varying(1024),
    "HostActionMode" character varying(32) DEFAULT 'none'::character varying NOT NULL,
    "ProductUrlTemplate" character varying(1024),
    "SearchUrlTemplate" character varying(1024),
    "AccessExpiresAt" timestamp with time zone
);


ALTER TABLE public."EmbedTargets" OWNER TO postgres;

--
-- Name: ErpInventorySnapshots; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."ErpInventorySnapshots" (
    "Id" uuid NOT NULL,
    "OwnerUserId" uuid NOT NULL,
    "ProductId" uuid,
    "Provider" character varying(64) NOT NULL,
    "ExternalProductId" character varying(128),
    "PartCode" character varying(128) NOT NULL,
    "ProductName" character varying(512) NOT NULL,
    "UnitPrice" numeric(18,2),
    "AvailableStock" integer,
    "Currency" character varying(8) NOT NULL,
    "IsActive" boolean NOT NULL,
    "LastSyncedAtUtc" timestamp with time zone NOT NULL,
    "LastWebhookReceivedAtUtc" timestamp with time zone NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone
);


ALTER TABLE public."ErpInventorySnapshots" OWNER TO postgres;

--
-- Name: ExternalProductLinkChecks; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."ExternalProductLinkChecks" (
    "Id" uuid NOT NULL,
    "ExternalProductId" uuid NOT NULL,
    "CheckedAtUtc" timestamp with time zone NOT NULL,
    "Method" character varying(8) NOT NULL,
    "StatusCode" integer,
    "IsReachable" boolean NOT NULL,
    "FinalUrl" character varying(2048),
    "ErrorSummary" text,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone
);


ALTER TABLE public."ExternalProductLinkChecks" OWNER TO postgres;

--
-- Name: ExternalProductOemNumbers; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."ExternalProductOemNumbers" (
    "Id" uuid NOT NULL,
    "ExternalProductId" uuid NOT NULL,
    "NormalizedOemNumber" character varying(128) NOT NULL,
    "OriginalOemNumber" character varying(128) NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone
);


ALTER TABLE public."ExternalProductOemNumbers" OWNER TO postgres;

--
-- Name: ExternalProducts; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."ExternalProducts" (
    "Id" uuid NOT NULL,
    "ExternalSiteId" uuid NOT NULL,
    "LastSeenInCrawlId" uuid,
    "SourceUrl" character varying(2048) NOT NULL,
    "CanonicalUrl" character varying(2048),
    "Title" character varying(512),
    "Sku" character varying(128),
    "PartCode" character varying(128),
    "Brand" character varying(160),
    "CategoryPathJson" text,
    "ImageUrl" character varying(2048),
    "AvailabilityText" text,
    "PriceText" text,
    "Currency" character varying(8),
    "RawPayloadJson" text,
    "IsActive" boolean NOT NULL,
    "LastSeenAtUtc" timestamp with time zone,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone
);


ALTER TABLE public."ExternalProducts" OWNER TO postgres;

--
-- Name: ExternalSiteCrawls; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."ExternalSiteCrawls" (
    "Id" uuid NOT NULL,
    "ExternalSiteId" uuid NOT NULL,
    "TriggerType" character varying(32) NOT NULL,
    "ExecutionMode" character varying(32) NOT NULL,
    "Status" character varying(32) NOT NULL,
    "StartedAtUtc" timestamp with time zone,
    "CompletedAtUtc" timestamp with time zone,
    "ProductCount" integer NOT NULL,
    "SkuCoverage" numeric(5,2),
    "OemCoverage" numeric(5,2),
    "ErrorSummary" text,
    "RawStatsJson" text,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone
);


ALTER TABLE public."ExternalSiteCrawls" OWNER TO postgres;

--
-- Name: ExternalSites; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."ExternalSites" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Name" character varying(160) NOT NULL,
    "BaseUrl" character varying(1024) NOT NULL,
    "Status" character varying(32) NOT NULL,
    "PreferredCrawlMode" character varying(32) NOT NULL,
    "LastCrawlAtUtc" timestamp with time zone,
    "LastSuccessfulCrawlAtUtc" timestamp with time zone,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone
);


ALTER TABLE public."ExternalSites" OWNER TO postgres;

--
-- Name: Folders; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."Folders" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "UserId" uuid NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL
);


ALTER TABLE public."Folders" OWNER TO postgres;

--
-- Name: Hotspots; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."Hotspots" (
    "Id" uuid NOT NULL,
    "Top" double precision NOT NULL,
    "Width" double precision NOT NULL,
    "PageId" uuid NOT NULL,
    "ProductId" uuid,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone,
    "AiConfidence" double precision DEFAULT 0.0 NOT NULL,
    "Height" double precision DEFAULT 0.0 NOT NULL,
    "IsAiDetected" boolean DEFAULT false NOT NULL,
    "Label" text,
    "Left" double precision DEFAULT 0.0 NOT NULL
);


ALTER TABLE public."Hotspots" OWNER TO postgres;

--
-- Name: ManualImportFiles; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."ManualImportFiles" (
    "Id" uuid NOT NULL,
    "ExternalSiteId" uuid NOT NULL,
    "FileName" character varying(256) NOT NULL,
    "FileType" character varying(16) NOT NULL,
    "StoragePath" character varying(1024) NOT NULL,
    "ImportedAtUtc" timestamp with time zone NOT NULL,
    "ImportedByUserId" uuid NOT NULL,
    "RowCount" integer NOT NULL,
    "Status" character varying(32) NOT NULL,
    "ErrorSummary" character varying(2048),
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone
);


ALTER TABLE public."ManualImportFiles" OWNER TO postgres;

--
-- Name: OrderItems; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."OrderItems" (
    "Id" uuid NOT NULL,
    "OrderId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "Quantity" integer NOT NULL,
    "UnitPrice" numeric(18,2) NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone
);


ALTER TABLE public."OrderItems" OWNER TO postgres;

--
-- Name: OrderStatusHistory; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."OrderStatusHistory" (
    "Id" uuid NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone,
    "OrderId" uuid NOT NULL,
    "PreviousStatus" integer,
    "NewStatus" integer NOT NULL,
    "IsVisibleToCustomer" boolean DEFAULT true NOT NULL,
    "Source" character varying(64) NOT NULL,
    "Note" character varying(512),
    "ChangedBy" character varying(256)
);


ALTER TABLE public."OrderStatusHistory" OWNER TO postgres;

--
-- Name: Orders; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."Orders" (
    "Id" uuid NOT NULL,
    "OrderNumber" text NOT NULL,
    "CustomerName" text NOT NULL,
    "CustomerPhone" text NOT NULL,
    "CustomerEmail" text NOT NULL,
    "CompanyName" text,
    "TotalAmount" numeric(18,2) NOT NULL,
    "Status" integer NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone,
    "CustomerId" uuid,
    "DeliveryAddress" text DEFAULT ''::text NOT NULL,
    "DeliveryCity" text DEFAULT ''::text NOT NULL,
    "DeliveryDistrict" text,
    "DeliveryNote" text,
    "PaymentMethod" text DEFAULT 'KapidaOdeme'::text NOT NULL,
    "IdempotencyKey" character varying(128),
    "OwnerUserId" uuid
);


ALTER TABLE public."Orders" OWNER TO postgres;

--
-- Name: PlatformAuditLogs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."PlatformAuditLogs" (
    "Id" uuid NOT NULL,
    "ActorUserId" uuid,
    "TargetOwnerUserId" uuid,
    "Action" character varying(128) NOT NULL,
    "ActorEmail" character varying(256),
    "ActorRole" character varying(64),
    "IpAddress" character varying(64),
    "UserAgent" character varying(512),
    "Payload" jsonb,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone
);


ALTER TABLE public."PlatformAuditLogs" OWNER TO postgres;

--
-- Name: Products; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."Products" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Code" text NOT NULL,
    "Description" text NOT NULL,
    "Price" numeric NOT NULL,
    "StockQuantity" integer NOT NULL,
    "Category" text NOT NULL,
    "CatalogId" uuid NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone,
    "PageNumber" text DEFAULT ''::text NOT NULL,
    "RefNo" integer DEFAULT 0 NOT NULL,
    "PageId" uuid,
    "ImageUrl" text,
    "OemNo" text
);


ALTER TABLE public."Products" OWNER TO postgres;

--
-- Name: PublicAccessLinks; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."PublicAccessLinks" (
    "Id" uuid NOT NULL,
    "TokenHash" text NOT NULL,
    "UserId" uuid NOT NULL,
    "PublicLinkVersion" integer NOT NULL,
    "CatalogIds" text,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "IsRevoked" boolean DEFAULT false NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone,
    "Token" text
);


ALTER TABLE public."PublicAccessLinks" OWNER TO postgres;

--
-- Name: PublicStorefrontViews; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."PublicStorefrontViews" (
    "Id" uuid NOT NULL,
    "OwnerUserId" uuid NOT NULL,
    "FingerprintHash" text NOT NULL,
    "BucketStartUtc" timestamp with time zone NOT NULL,
    "ViewedAtUtc" timestamp with time zone NOT NULL,
    "Source" text NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone
);


ALTER TABLE public."PublicStorefrontViews" OWNER TO postgres;

--
-- Name: StockMovements; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."StockMovements" (
    "Id" uuid NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone,
    "UserId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "ProductCode" character varying(128) NOT NULL,
    "ProductName" character varying(512) NOT NULL,
    "PreviousQuantity" integer NOT NULL,
    "DeltaQuantity" integer NOT NULL,
    "NewQuantity" integer NOT NULL,
    "MovementType" character varying(32) NOT NULL,
    "Reason" character varying(1024) NOT NULL,
    "Source" character varying(128),
    "ActorName" character varying(256),
    "ReferenceId" character varying(128)
);


ALTER TABLE public."StockMovements" OWNER TO postgres;

--
-- Name: UserAiUsageMonthly; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."UserAiUsageMonthly" (
    "UserId" uuid NOT NULL,
    "MonthStartUtc" timestamp with time zone NOT NULL,
    "QueryCount" integer DEFAULT 0 NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone
);


ALTER TABLE public."UserAiUsageMonthly" OWNER TO postgres;

--
-- Name: Users; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."Users" (
    "Id" uuid NOT NULL,
    "FirstName" text NOT NULL,
    "LastName" text NOT NULL,
    "Email" text NOT NULL,
    "PasswordHash" text NOT NULL,
    "Role" text NOT NULL,
    "CompanyName" text,
    "CreatedDate" timestamp with time zone NOT NULL,
    "UpdatedDate" timestamp with time zone,
    "PublicLinkEnabled" boolean DEFAULT true NOT NULL,
    "PublicLinkVersion" integer DEFAULT 1 NOT NULL,
    "PhoneNumber" text,
    "PasswordSalt" text,
    "MaxCatalogCount" integer DEFAULT 3 NOT NULL,
    "MaxPagePerCatalog" integer DEFAULT 100 NOT NULL,
    "PlanActivatedAt" timestamp with time zone,
    "PlanExpiresAt" timestamp with time zone,
    "SubscriptionPlan" integer DEFAULT 1 NOT NULL,
    "PublicStoreSlug" character varying(96),
    "IsApproved" boolean DEFAULT true NOT NULL
);


ALTER TABLE public."Users" OWNER TO postgres;

--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


ALTER TABLE public."__EFMigrationsHistory" OWNER TO postgres;

--
-- Name: aggregatedcounter id; Type: DEFAULT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.aggregatedcounter ALTER COLUMN id SET DEFAULT nextval('hangfire.aggregatedcounter_id_seq'::regclass);


--
-- Name: counter id; Type: DEFAULT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.counter ALTER COLUMN id SET DEFAULT nextval('hangfire.counter_id_seq'::regclass);


--
-- Name: hash id; Type: DEFAULT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.hash ALTER COLUMN id SET DEFAULT nextval('hangfire.hash_id_seq'::regclass);


--
-- Name: job id; Type: DEFAULT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.job ALTER COLUMN id SET DEFAULT nextval('hangfire.job_id_seq'::regclass);


--
-- Name: jobparameter id; Type: DEFAULT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.jobparameter ALTER COLUMN id SET DEFAULT nextval('hangfire.jobparameter_id_seq'::regclass);


--
-- Name: jobqueue id; Type: DEFAULT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.jobqueue ALTER COLUMN id SET DEFAULT nextval('hangfire.jobqueue_id_seq'::regclass);


--
-- Name: list id; Type: DEFAULT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.list ALTER COLUMN id SET DEFAULT nextval('hangfire.list_id_seq'::regclass);


--
-- Name: set id; Type: DEFAULT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.set ALTER COLUMN id SET DEFAULT nextval('hangfire.set_id_seq'::regclass);


--
-- Name: state id; Type: DEFAULT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.state ALTER COLUMN id SET DEFAULT nextval('hangfire.state_id_seq'::regclass);


--
-- Data for Name: aggregatedcounter; Type: TABLE DATA; Schema: hangfire; Owner: postgres
--

COPY hangfire.aggregatedcounter (id, key, value, expireat) FROM stdin;
2	stats:failed:2026-04-20	2	2026-05-20 12:00:44.721712+00
5	stats:succeeded:2026-04-20	2	2026-05-20 21:53:03.364652+00
10	stats:succeeded:2026-04-21-21	2	2026-04-22 21:00:05.395993+00
11	stats:succeeded:2026-04-21	2	2026-05-21 21:00:04.395993+00
6	stats:succeeded	4	\N
\.


--
-- Data for Name: counter; Type: TABLE DATA; Schema: hangfire; Owner: postgres
--

COPY hangfire.counter (id, key, value, expireat) FROM stdin;
\.


--
-- Data for Name: hash; Type: TABLE DATA; Schema: hangfire; Owner: postgres
--

COPY hangfire.hash (id, key, field, value, expireat, updatecount) FROM stdin;
1	recurring-job:recheck-broken-links	Queue	default	\N	0
2	recurring-job:recheck-broken-links	Cron	0 0 * * *	\N	0
3	recurring-job:recheck-broken-links	TimeZoneId	Europe/Istanbul	\N	0
4	recurring-job:recheck-broken-links	Job	{"t":"Katalogcu.API.Services.ExternalLinkRecheckHangfireJob, Katalogcu.API","m":"ExecuteAsync","p":["System.Threading.CancellationToken, mscorlib"],"a":[null]}	\N	0
5	recurring-job:recheck-broken-links	CreatedAt	1776668048949	\N	0
7	recurring-job:recheck-broken-links	V	2	\N	0
8	recurring-job:recheck-broken-links	LastExecution	1776805205338	\N	0
6	recurring-job:recheck-broken-links	NextExecution	1776891600000	\N	0
9	recurring-job:recheck-broken-links	LastJobId	4	\N	0
\.


--
-- Data for Name: job; Type: TABLE DATA; Schema: hangfire; Owner: postgres
--

COPY hangfire.job (id, stateid, statename, invocationdata, arguments, createdat, expireat, updatecount) FROM stdin;
1	85	Failed	{"Type": "Katalogcu.API.Services.CatalogAiHangfireJob, Katalogcu.API", "Method": "ExecuteAsync", "Arguments": "[\\"\\\\\\"bb8b7d55-4d84-4ec9-b3bf-883902838414\\\\\\"\\",null]", "ParameterTypes": "[\\"System.Guid, mscorlib\\",\\"System.Threading.CancellationToken, mscorlib\\"]"}	["\\"bb8b7d55-4d84-4ec9-b3bf-883902838414\\"", null]	2026-04-20 06:55:29.669035+00	\N	0
4	92	Succeeded	{"Type": "Katalogcu.API.Services.ExternalLinkRecheckHangfireJob, Katalogcu.API", "Method": "ExecuteAsync", "Arguments": "[null]", "ParameterTypes": "[\\"System.Threading.CancellationToken, mscorlib\\"]"}	[null]	2026-04-21 21:00:05.353263+00	2026-04-22 21:00:05.395993+00	0
2	86	Failed	{"Type": "Katalogcu.API.Services.CatalogAiHangfireJob, Katalogcu.API", "Method": "ExecuteAsync", "Arguments": "[\\"\\\\\\"e9e4447e-8238-411a-96a6-5e2d94124972\\\\\\"\\",null]", "ParameterTypes": "[\\"System.Guid, mscorlib\\",\\"System.Threading.CancellationToken, mscorlib\\"]"}	["\\"e9e4447e-8238-411a-96a6-5e2d94124972\\"", null]	2026-04-20 06:58:05.296869+00	\N	0
\.


--
-- Data for Name: jobparameter; Type: TABLE DATA; Schema: hangfire; Owner: postgres
--

COPY hangfire.jobparameter (id, jobid, name, value, updatecount) FROM stdin;
1	1	CurrentCulture	""	0
3	2	CurrentCulture	""	0
4	2	RetryCount	10	0
2	1	RetryCount	10	0
8	4	RecurringJobId	"recheck-broken-links"	0
9	4	Time	1776805205	0
10	4	CurrentCulture	""	0
\.


--
-- Data for Name: jobqueue; Type: TABLE DATA; Schema: hangfire; Owner: postgres
--

COPY hangfire.jobqueue (id, jobid, queue, fetchedat, updatecount) FROM stdin;
\.


--
-- Data for Name: list; Type: TABLE DATA; Schema: hangfire; Owner: postgres
--

COPY hangfire.list (id, key, value, expireat, updatecount) FROM stdin;
\.


--
-- Data for Name: lock; Type: TABLE DATA; Schema: hangfire; Owner: postgres
--

COPY hangfire.lock (resource, updatecount, acquired) FROM stdin;
\.


--
-- Data for Name: schema; Type: TABLE DATA; Schema: hangfire; Owner: postgres
--

COPY hangfire.schema (version) FROM stdin;
22
\.


--
-- Data for Name: server; Type: TABLE DATA; Schema: hangfire; Owner: postgres
--

COPY hangfire.server (id, data, lastheartbeat, updatecount) FROM stdin;
95ac72740e71:external-site-crawl:1:436fed9c-c86e-42e0-b14a-f67210465527	{"Queues": ["external-site-crawl"], "StartedAt": "2026-04-21T16:42:41.1269191Z", "WorkerCount": 1}	2026-04-21 22:37:17.438487+00	0
95ac72740e71:catalog-ai:1:7cd3b420-24c2-4468-bab0-e3dc953be7fc	{"Queues": ["catalog-ai"], "StartedAt": "2026-04-21T16:42:41.1273958Z", "WorkerCount": 3}	2026-04-21 22:37:17.540578+00	0
95ac72740e71:default:1:946ea3ba-76a2-4940-b57e-b4b6480ffdd0	{"Queues": ["external-link-recheck", "default"], "StartedAt": "2026-04-21T16:42:41.1274264Z", "WorkerCount": 1}	2026-04-21 22:37:17.438486+00	0
\.


--
-- Data for Name: set; Type: TABLE DATA; Schema: hangfire; Owner: postgres
--

COPY hangfire.set (id, key, score, value, expireat, updatecount) FROM stdin;
1	recurring-jobs	1776891600	recheck-broken-links	\N	0
\.


--
-- Data for Name: state; Type: TABLE DATA; Schema: hangfire; Owner: postgres
--

COPY hangfire.state (id, jobid, name, reason, createdat, data, updatecount) FROM stdin;
1	1	Enqueued	\N	2026-04-20 06:55:29.688739+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776668129661"}	0
2	1	Processing	\N	2026-04-20 06:55:29.720456+00	{"ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "WorkerId": "9182141c-77bd-4c73-ae62-cc0ca1a1acfc", "StartedAt": "1776668129700"}	0
3	1	Failed	An exception occurred during performance of the job.	2026-04-20 06:55:29.795333+00	{"FailedAt": "1776668129768", "ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414."}	0
4	1	Scheduled	Retry attempt 1 of 10: Catalog processing returned failure for catalog b…	2026-04-20 06:55:29.79777+00	{"EnqueueAt": "1776668160792", "ScheduledAt": "1776668129792"}	0
5	1	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 06:56:10.866509+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776668170858"}	0
6	1	Processing	\N	2026-04-20 06:56:10.873929+00	{"ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "WorkerId": "c9c8ec39-0270-4f05-9476-56943d12dd9f", "StartedAt": "1776668170871"}	0
7	1	Failed	An exception occurred during performance of the job.	2026-04-20 06:56:10.900938+00	{"FailedAt": "1776668170893", "ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414."}	0
8	1	Scheduled	Retry attempt 2 of 10: Catalog processing returned failure for catalog b…	2026-04-20 06:56:10.901953+00	{"EnqueueAt": "1776668204900", "ScheduledAt": "1776668170900"}	0
9	1	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 06:56:55.904309+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776668215898"}	0
10	1	Processing	\N	2026-04-20 06:56:55.912169+00	{"ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "WorkerId": "c9c8ec39-0270-4f05-9476-56943d12dd9f", "StartedAt": "1776668215909"}	0
11	1	Failed	An exception occurred during performance of the job.	2026-04-20 06:56:55.948416+00	{"FailedAt": "1776668215938", "ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414."}	0
12	1	Scheduled	Retry attempt 3 of 10: Catalog processing returned failure for catalog b…	2026-04-20 06:56:55.94949+00	{"EnqueueAt": "1776668270947", "ScheduledAt": "1776668215947"}	0
13	1	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 06:57:55.942788+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776668275938"}	0
14	1	Processing	\N	2026-04-20 06:57:55.951069+00	{"ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "WorkerId": "c9c8ec39-0270-4f05-9476-56943d12dd9f", "StartedAt": "1776668275947"}	0
15	1	Failed	An exception occurred during performance of the job.	2026-04-20 06:57:55.97311+00	{"FailedAt": "1776668275965", "ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414."}	0
16	1	Scheduled	Retry attempt 4 of 10: Catalog processing returned failure for catalog b…	2026-04-20 06:57:55.974065+00	{"EnqueueAt": "1776668471972", "ScheduledAt": "1776668275972"}	0
17	2	Enqueued	\N	2026-04-20 06:58:05.299269+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776668285296"}	0
18	2	Processing	\N	2026-04-20 06:58:05.304904+00	{"ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "WorkerId": "1da8884c-924a-4783-8cb2-ce4fb58fca1e", "StartedAt": "1776668285302"}	0
19	2	Failed	An exception occurred during performance of the job.	2026-04-20 06:58:05.339772+00	{"FailedAt": "1776668285329", "ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972."}	0
20	2	Scheduled	Retry attempt 1 of 10: Catalog processing returned failure for catalog e…	2026-04-20 06:58:05.340629+00	{"EnqueueAt": "1776668329339", "ScheduledAt": "1776668285339"}	0
21	2	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 06:58:55.994772+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776668335987"}	0
22	2	Processing	\N	2026-04-20 06:58:56.003475+00	{"ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "WorkerId": "9182141c-77bd-4c73-ae62-cc0ca1a1acfc", "StartedAt": "1776668336000"}	0
23	2	Failed	An exception occurred during performance of the job.	2026-04-20 06:58:56.028245+00	{"FailedAt": "1776668336020", "ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972."}	0
24	2	Scheduled	Retry attempt 2 of 10: Catalog processing returned failure for catalog e…	2026-04-20 06:58:56.029439+00	{"EnqueueAt": "1776668410027", "ScheduledAt": "1776668336027"}	0
25	2	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 07:00:11.019937+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776668411015"}	0
26	2	Processing	\N	2026-04-20 07:00:11.029332+00	{"ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "WorkerId": "9182141c-77bd-4c73-ae62-cc0ca1a1acfc", "StartedAt": "1776668411025"}	0
27	2	Failed	An exception occurred during performance of the job.	2026-04-20 07:00:11.059731+00	{"FailedAt": "1776668411053", "ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972."}	0
28	2	Scheduled	Retry attempt 3 of 10: Catalog processing returned failure for catalog e…	2026-04-20 07:00:11.060691+00	{"EnqueueAt": "1776668508059", "ScheduledAt": "1776668411059"}	0
29	1	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 07:01:11.068142+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776668471061"}	0
30	1	Processing	\N	2026-04-20 07:01:11.074397+00	{"ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "WorkerId": "9182141c-77bd-4c73-ae62-cc0ca1a1acfc", "StartedAt": "1776668471072"}	0
31	1	Failed	An exception occurred during performance of the job.	2026-04-20 07:01:11.09837+00	{"FailedAt": "1776668471090", "ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414."}	0
32	1	Scheduled	Retry attempt 5 of 10: Catalog processing returned failure for catalog b…	2026-04-20 07:01:11.099363+00	{"EnqueueAt": "1776668762097", "ScheduledAt": "1776668471097"}	0
33	2	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 07:01:56.104661+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776668516096"}	0
34	2	Processing	\N	2026-04-20 07:01:56.113532+00	{"ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "WorkerId": "9182141c-77bd-4c73-ae62-cc0ca1a1acfc", "StartedAt": "1776668516110"}	0
35	2	Failed	An exception occurred during performance of the job.	2026-04-20 07:01:56.139511+00	{"FailedAt": "1776668516131", "ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972."}	0
36	2	Scheduled	Retry attempt 4 of 10: Catalog processing returned failure for catalog e…	2026-04-20 07:01:56.140431+00	{"EnqueueAt": "1776668684139", "ScheduledAt": "1776668516139"}	0
37	2	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 07:04:56.201651+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776668696190"}	0
38	2	Processing	\N	2026-04-20 07:04:56.21175+00	{"ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "WorkerId": "1da8884c-924a-4783-8cb2-ce4fb58fca1e", "StartedAt": "1776668696208"}	0
39	2	Failed	An exception occurred during performance of the job.	2026-04-20 07:04:56.245961+00	{"FailedAt": "1776668696239", "ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972."}	0
40	2	Scheduled	Retry attempt 5 of 10: Catalog processing returned failure for catalog e…	2026-04-20 07:04:56.246794+00	{"EnqueueAt": "1776669097245", "ScheduledAt": "1776668696245"}	0
41	1	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 07:06:11.265463+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776668771258"}	0
42	1	Processing	\N	2026-04-20 07:06:11.2744+00	{"ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "WorkerId": "1da8884c-924a-4783-8cb2-ce4fb58fca1e", "StartedAt": "1776668771271"}	0
43	1	Failed	An exception occurred during performance of the job.	2026-04-20 07:06:11.306792+00	{"FailedAt": "1776668771299", "ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414."}	0
44	1	Scheduled	Retry attempt 6 of 10: Catalog processing returned failure for catalog b…	2026-04-20 07:06:11.307712+00	{"EnqueueAt": "1776669555306", "ScheduledAt": "1776668771306"}	0
45	2	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 07:11:41.45298+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776669101436"}	0
46	2	Processing	\N	2026-04-20 07:11:41.476723+00	{"ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "WorkerId": "c9c8ec39-0270-4f05-9476-56943d12dd9f", "StartedAt": "1776669101464"}	0
47	2	Failed	An exception occurred during performance of the job.	2026-04-20 07:11:41.547282+00	{"FailedAt": "1776669101527", "ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972."}	0
48	2	Scheduled	Retry attempt 6 of 10: Catalog processing returned failure for catalog e…	2026-04-20 07:11:41.552061+00	{"EnqueueAt": "1776669855546", "ScheduledAt": "1776669101546"}	0
49	1	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 07:19:26.68426+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776669566666"}	0
50	1	Processing	\N	2026-04-20 07:19:26.702369+00	{"ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "WorkerId": "9182141c-77bd-4c73-ae62-cc0ca1a1acfc", "StartedAt": "1776669566697"}	0
51	1	Failed	An exception occurred during performance of the job.	2026-04-20 07:19:26.819719+00	{"FailedAt": "1776669566799", "ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414."}	0
52	1	Scheduled	Retry attempt 7 of 10: Catalog processing returned failure for catalog b…	2026-04-20 07:19:26.822957+00	{"EnqueueAt": "1776671080818", "ScheduledAt": "1776669566818"}	0
53	2	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 07:24:26.88118+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776669866869"}	0
54	2	Processing	\N	2026-04-20 07:24:26.890497+00	{"ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "WorkerId": "9182141c-77bd-4c73-ae62-cc0ca1a1acfc", "StartedAt": "1776669866887"}	0
55	2	Failed	An exception occurred during performance of the job.	2026-04-20 07:24:26.954924+00	{"FailedAt": "1776669866942", "ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972."}	0
56	2	Scheduled	Retry attempt 7 of 10: Catalog processing returned failure for catalog e…	2026-04-20 07:24:26.956961+00	{"EnqueueAt": "1776671198954", "ScheduledAt": "1776669866954"}	0
57	1	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 07:44:42.539839+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776671082529"}	0
58	1	Processing	\N	2026-04-20 07:44:42.54923+00	{"ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "WorkerId": "1da8884c-924a-4783-8cb2-ce4fb58fca1e", "StartedAt": "1776671082546"}	0
59	1	Failed	An exception occurred during performance of the job.	2026-04-20 07:44:42.592034+00	{"FailedAt": "1776671082585", "ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414."}	0
60	1	Scheduled	Retry attempt 8 of 10: Catalog processing returned failure for catalog b…	2026-04-20 07:44:42.593072+00	{"EnqueueAt": "1776673618591", "ScheduledAt": "1776671082591"}	0
61	2	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 07:46:42.619043+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776671202613"}	0
62	2	Processing	\N	2026-04-20 07:46:42.628019+00	{"ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "WorkerId": "1da8884c-924a-4783-8cb2-ce4fb58fca1e", "StartedAt": "1776671202625"}	0
63	2	Failed	An exception occurred during performance of the job.	2026-04-20 07:46:42.651185+00	{"FailedAt": "1776671202643", "ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972."}	0
64	2	Scheduled	Retry attempt 8 of 10: Catalog processing returned failure for catalog e…	2026-04-20 07:46:42.652153+00	{"EnqueueAt": "1776673810650", "ScheduledAt": "1776671202650"}	0
65	1	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 08:29:43.08197+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776673783068"}	0
66	1	Processing	\N	2026-04-20 08:29:43.091986+00	{"ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "WorkerId": "c9c8ec39-0270-4f05-9476-56943d12dd9f", "StartedAt": "1776673783089"}	0
67	1	Failed	An exception occurred during performance of the job.	2026-04-20 08:29:43.156939+00	{"FailedAt": "1776673783148", "ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414."}	0
68	1	Scheduled	Retry attempt 9 of 10: Catalog processing returned failure for catalog b…	2026-04-20 08:29:43.158207+00	{"EnqueueAt": "1776678011156", "ScheduledAt": "1776673783156"}	0
69	2	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 08:45:38.12611+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776674738120"}	0
70	2	Processing	\N	2026-04-20 08:45:38.13292+00	{"ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "WorkerId": "c9c8ec39-0270-4f05-9476-56943d12dd9f", "StartedAt": "1776674738130"}	0
71	2	Failed	An exception occurred during performance of the job.	2026-04-20 08:45:38.152315+00	{"FailedAt": "1776674738145", "ServerId": "95ac72740e71:catalog-ai:1:90fecc54-255e-4e7c-88ff-743d7dce2bee", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972."}	0
72	2	Scheduled	Retry attempt 9 of 10: Catalog processing returned failure for catalog e…	2026-04-20 08:45:38.153132+00	{"EnqueueAt": "1776678930151", "ScheduledAt": "1776674738152"}	0
73	1	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 09:58:36.671485+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776679116668"}	0
74	2	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 09:58:36.675885+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776679116674"}	0
75	1	Processing	\N	2026-04-20 09:58:36.675867+00	{"ServerId": "95ac72740e71:catalog-ai:1:3b6b3249-154a-452d-b509-cacd16021bb8", "WorkerId": "ebd0f93c-869f-4964-bcef-4f1de6dbcd3c", "StartedAt": "1776679116674"}	0
76	2	Processing	\N	2026-04-20 09:58:36.680044+00	{"ServerId": "95ac72740e71:catalog-ai:1:3b6b3249-154a-452d-b509-cacd16021bb8", "WorkerId": "dea61f4d-83fc-4edd-b41d-05d6ee384b34", "StartedAt": "1776679116678"}	0
77	2	Failed	An exception occurred during performance of the job.	2026-04-20 09:58:36.707937+00	{"FailedAt": "1776679116700", "ServerId": "95ac72740e71:catalog-ai:1:3b6b3249-154a-452d-b509-cacd16021bb8", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972."}	0
78	1	Failed	An exception occurred during performance of the job.	2026-04-20 09:58:36.708318+00	{"FailedAt": "1776679116700", "ServerId": "95ac72740e71:catalog-ai:1:3b6b3249-154a-452d-b509-cacd16021bb8", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414."}	0
79	2	Scheduled	Retry attempt 10 of 10: Catalog processing returned failure for catalog e…	2026-04-20 09:58:36.708962+00	{"EnqueueAt": "1776685942707", "ScheduledAt": "1776679116707"}	0
80	1	Scheduled	Retry attempt 10 of 10: Catalog processing returned failure for catalog b…	2026-04-20 09:58:36.708986+00	{"EnqueueAt": "1776685912707", "ScheduledAt": "1776679116707"}	0
81	1	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 12:00:45.478257+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776686445471"}	0
82	2	Enqueued	Triggered by DelayedJobScheduler	2026-04-20 12:00:45.484016+00	{"Queue": "catalog-ai", "EnqueuedAt": "1776686445481"}	0
83	1	Processing	\N	2026-04-20 12:00:45.484612+00	{"ServerId": "95ac72740e71:catalog-ai:1:3b6b3249-154a-452d-b509-cacd16021bb8", "WorkerId": "472712a2-8c8e-401a-b3e7-5d276fc41d7d", "StartedAt": "1776686445482"}	0
84	2	Processing	\N	2026-04-20 12:00:45.489032+00	{"ServerId": "95ac72740e71:catalog-ai:1:3b6b3249-154a-452d-b509-cacd16021bb8", "WorkerId": "ebd0f93c-869f-4964-bcef-4f1de6dbcd3c", "StartedAt": "1776686445486"}	0
85	1	Failed	An exception occurred during performance of the job.	2026-04-20 12:00:45.717852+00	{"FailedAt": "1776686445697", "ServerId": "95ac72740e71:catalog-ai:1:3b6b3249-154a-452d-b509-cacd16021bb8", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414."}	0
86	2	Failed	An exception occurred during performance of the job.	2026-04-20 12:00:45.722294+00	{"FailedAt": "1776686445711", "ServerId": "95ac72740e71:catalog-ai:1:3b6b3249-154a-452d-b509-cacd16021bb8", "ExceptionType": "System.InvalidOperationException", "ExceptionDetails": "System.InvalidOperationException: Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972.\\n   at Katalogcu.API.Services.CatalogAiHangfireJob.ExecuteAsync(Guid catalogId, CancellationToken cancellationToken) in /src/Katalogcu.API/Services/CatalogAiHangfireJob.cs:line 32\\n   at InvokeStub_TaskAwaiter.GetResult(Object, Object, IntPtr*)\\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)", "ExceptionMessage": "Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972."}	0
90	4	Enqueued	Triggered by recurring job scheduler	2026-04-21 21:00:05.36036+00	{"Queue": "external-link-recheck", "EnqueuedAt": "1776805205360"}	0
91	4	Processing	\N	2026-04-21 21:00:05.368738+00	{"ServerId": "95ac72740e71:default:1:946ea3ba-76a2-4940-b57e-b4b6480ffdd0", "WorkerId": "0d22b95f-21a1-40f8-9e13-39ef913bef62", "StartedAt": "1776805205366"}	0
92	4	Succeeded	\N	2026-04-21 21:00:05.396591+00	{"Latency": "17", "SucceededAt": "1776805205393", "PerformanceDuration": "22"}	0
\.


--
-- Data for Name: CatalogAiJobs; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."CatalogAiJobs" ("Id", "CreatedDate", "UpdatedDate", "CatalogId", "Status", "AttemptCount", "MaxAttempts", "NextAttemptAt", "LastAttemptAt", "LockedUntil", "LastError") FROM stdin;
5c763176-3324-4489-98a6-118d423b8142	2026-04-20 06:55:29.610286+00	2026-04-20 12:00:45.70269+00	bb8b7d55-4d84-4ec9-b3bf-883902838414	Failed	11	4	2026-04-20 12:00:45.602382+00	2026-04-20 12:00:45.602382+00	\N	Catalog processing returned failure for catalog bb8b7d55-4d84-4ec9-b3bf-883902838414.
3b931f2d-522d-429f-9c4a-c3c6298215c6	2026-04-20 06:58:05.293258+00	2026-04-20 12:00:45.717996+00	e9e4447e-8238-411a-96a6-5e2d94124972	Failed	11	4	2026-04-20 12:00:45.686581+00	2026-04-20 12:00:45.686581+00	\N	Catalog processing returned failure for catalog e9e4447e-8238-411a-96a6-5e2d94124972.
\.


--
-- Data for Name: CatalogItemExternalMatches; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."CatalogItemExternalMatches" ("Id", "CatalogId", "CatalogPageId", "CatalogItemId", "ExternalSiteId", "ExternalProductId", "ExternalProductUrl", "ExternalProductTitle", "ConfidenceScore", "Status", "MatchedBy", "IsActive", "MatchedAtUtc", "ReviewedByUserId", "ReviewedAtUtc", "ReviewNote", "MatchReasonsJson", "LastLinkCheckAtUtc", "LastLinkStatusCode", "IsLinkHealthy", "CreatedDate", "UpdatedDate") FROM stdin;
\.


--
-- Data for Name: CatalogItems; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."CatalogItems" ("Id", "CatalogId", "PageNumber", "RefNumber", "PartCode", "PartName", "Description", "CreatedDate", "UpdatedDate", "Embedding", "Dimensions", "MachineBrand", "MachineModel", "Mechanism", "MachineGroup", "VisualBbox", "VisualEmbedding", "VisualOcrText", "VisualPageNumber", "VisualShapeTags", "VisualImageUrl") FROM stdin;
\.


--
-- Data for Name: CatalogPages; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."CatalogPages" ("Id", "PageNumber", "ImageUrl", "CatalogId", "CreatedDate", "UpdatedDate", "AiDescription", "IsTechnicalDrawing", "ReviewedAt", "ReviewNotes", "ReviewStatus") FROM stdin;
\.


--
-- Data for Name: CatalogViews; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."CatalogViews" ("Id", "CatalogId", "OwnerUserId", "FingerprintHash", "BucketStartUtc", "ViewedAtUtc", "Source", "CreatedDate", "UpdatedDate") FROM stdin;
\.


--
-- Data for Name: Catalogs; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."Catalogs" ("Id", "Name", "Description", "ImageUrl", "PdfUrl", "Status", "UserId", "CreatedDate", "UpdatedDate", "FolderId") FROM stdin;
d69b87b0-f1f8-4418-9739-32a23ba0a4ed	Public Smoke Catalog	Public link smoke			Published	ec5dabf2-b16f-45e2-952c-6e684beab2ca	2026-04-20 06:55:54.124462+00	2026-04-20 06:55:54.247284+00	\N
bb8b7d55-4d84-4ec9-b3bf-883902838414	Smoke Catalog	Catalog-only staging smoke			Error	ec5dabf2-b16f-45e2-952c-6e684beab2ca	2026-04-20 06:55:28.693013+00	2026-04-20 12:00:45.706009+00	\N
e9e4447e-8238-411a-96a6-5e2d94124972	Smoke Catalog	Postdeploy smoke catalog			Error	ec5dabf2-b16f-45e2-952c-6e684beab2ca	2026-04-20 06:58:05.15386+00	2026-04-20 12:00:45.719675+00	\N
\.


--
-- Data for Name: Customers; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."Customers" ("Id", "UserId", "FullName", "Phone", "NormalizedPhone", "Email", "CompanyName", "Note", "LastVisitDate", "LastOrderDate", "OrderCount", "TotalSpent", "IsActive", "CreatedDate", "UpdatedDate", "LastLoginDate", "LoginCode", "LoginCodeExpiresAt", "PublicSessionExpiresAt", "PublicSessionToken", "PasswordHash", "PasswordSalt", "FailedLoginCount", "LoginLockoutUntil") FROM stdin;
\.


--
-- Data for Name: EmbedSettings; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."EmbedSettings" ("Id", "UserId", "AllowedOrigins", "Theme", "Mode", "CreatedDate", "UpdatedDate") FROM stdin;
2cbe6b33-d56f-47b5-9982-624aa94c291c	ec5dabf2-b16f-45e2-952c-6e684beab2ca	["http://localhost:4200"]	default	catalog	2026-04-20 06:55:29.747046+00	2026-04-20 06:58:05.331181+00
\.


--
-- Data for Name: EmbedTargets; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."EmbedTargets" ("Id", "UserId", "Name", "Type", "CatalogId", "CatalogPageId", "EmbedKey", "CommerceMode", "IsActive", "CreatedDate", "UpdatedDate", "ExistingCartMethod", "ExistingCartUrl", "HostActionMode", "ProductUrlTemplate", "SearchUrlTemplate", "AccessExpiresAt") FROM stdin;
0c417b50-8021-4150-b812-940d0f13d86c	ec5dabf2-b16f-45e2-952c-6e684beab2ca	Smoke Embed	catalog	bb8b7d55-4d84-4ec9-b3bf-883902838414	\N	emb_77d7696cb4af412eab305f3350f8924f	catalog_only	t	2026-04-20 06:55:29.780722+00	\N	POST	\N	none	\N	\N	\N
dee3c9c5-73b8-421c-bd8a-49fb1d22a318	ec5dabf2-b16f-45e2-952c-6e684beab2ca	Smoke Embed	catalog	e9e4447e-8238-411a-96a6-5e2d94124972	\N	emb_9c348b2a70324eb4bd34ef8d28ce3dfe	catalog_only	t	2026-04-20 06:58:05.348634+00	\N	POST	\N	none	\N	\N	\N
\.


--
-- Data for Name: ErpInventorySnapshots; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."ErpInventorySnapshots" ("Id", "OwnerUserId", "ProductId", "Provider", "ExternalProductId", "PartCode", "ProductName", "UnitPrice", "AvailableStock", "Currency", "IsActive", "LastSyncedAtUtc", "LastWebhookReceivedAtUtc", "CreatedDate", "UpdatedDate") FROM stdin;
\.


--
-- Data for Name: ExternalProductLinkChecks; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."ExternalProductLinkChecks" ("Id", "ExternalProductId", "CheckedAtUtc", "Method", "StatusCode", "IsReachable", "FinalUrl", "ErrorSummary", "CreatedDate", "UpdatedDate") FROM stdin;
\.


--
-- Data for Name: ExternalProductOemNumbers; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."ExternalProductOemNumbers" ("Id", "ExternalProductId", "NormalizedOemNumber", "OriginalOemNumber", "CreatedDate", "UpdatedDate") FROM stdin;
\.


--
-- Data for Name: ExternalProducts; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."ExternalProducts" ("Id", "ExternalSiteId", "LastSeenInCrawlId", "SourceUrl", "CanonicalUrl", "Title", "Sku", "PartCode", "Brand", "CategoryPathJson", "ImageUrl", "AvailabilityText", "PriceText", "Currency", "RawPayloadJson", "IsActive", "LastSeenAtUtc", "CreatedDate", "UpdatedDate") FROM stdin;
\.


--
-- Data for Name: ExternalSiteCrawls; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."ExternalSiteCrawls" ("Id", "ExternalSiteId", "TriggerType", "ExecutionMode", "Status", "StartedAtUtc", "CompletedAtUtc", "ProductCount", "SkuCoverage", "OemCoverage", "ErrorSummary", "RawStatsJson", "CreatedDate", "UpdatedDate") FROM stdin;
\.


--
-- Data for Name: ExternalSites; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."ExternalSites" ("Id", "UserId", "Name", "BaseUrl", "Status", "PreferredCrawlMode", "LastCrawlAtUtc", "LastSuccessfulCrawlAtUtc", "CreatedDate", "UpdatedDate") FROM stdin;
7ae8a4c6-e070-43ca-b061-60001d32e035	ec5dabf2-b16f-45e2-952c-6e684beab2ca	Smoke External Site	https://example.com	active	manual_import	\N	\N	2026-04-20 06:55:29.356723+00	\N
787ccb20-8772-46a3-956c-40ccf06ccf52	ec5dabf2-b16f-45e2-952c-6e684beab2ca	Smoke External Site 1776668284-70336	https://smoke-1776668284-70336.example.com	active	manual_import	\N	\N	2026-04-20 06:58:05.001646+00	\N
\.


--
-- Data for Name: Folders; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."Folders" ("Id", "Name", "UserId", "CreatedDate") FROM stdin;
\.


--
-- Data for Name: Hotspots; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."Hotspots" ("Id", "Top", "Width", "PageId", "ProductId", "CreatedDate", "UpdatedDate", "AiConfidence", "Height", "IsAiDetected", "Label", "Left") FROM stdin;
\.


--
-- Data for Name: ManualImportFiles; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."ManualImportFiles" ("Id", "ExternalSiteId", "FileName", "FileType", "StoragePath", "ImportedAtUtc", "ImportedByUserId", "RowCount", "Status", "ErrorSummary", "CreatedDate", "UpdatedDate") FROM stdin;
\.


--
-- Data for Name: OrderItems; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."OrderItems" ("Id", "OrderId", "ProductId", "Quantity", "UnitPrice", "CreatedDate", "UpdatedDate") FROM stdin;
\.


--
-- Data for Name: OrderStatusHistory; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."OrderStatusHistory" ("Id", "CreatedDate", "UpdatedDate", "OrderId", "PreviousStatus", "NewStatus", "IsVisibleToCustomer", "Source", "Note", "ChangedBy") FROM stdin;
\.


--
-- Data for Name: Orders; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."Orders" ("Id", "OrderNumber", "CustomerName", "CustomerPhone", "CustomerEmail", "CompanyName", "TotalAmount", "Status", "CreatedDate", "UpdatedDate", "CustomerId", "DeliveryAddress", "DeliveryCity", "DeliveryDistrict", "DeliveryNote", "PaymentMethod", "IdempotencyKey", "OwnerUserId") FROM stdin;
\.


--
-- Data for Name: PlatformAuditLogs; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."PlatformAuditLogs" ("Id", "ActorUserId", "TargetOwnerUserId", "Action", "ActorEmail", "ActorRole", "IpAddress", "UserAgent", "Payload", "CreatedDate", "UpdatedDate") FROM stdin;
\.


--
-- Data for Name: Products; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."Products" ("Id", "Name", "Code", "Description", "Price", "StockQuantity", "Category", "CatalogId", "CreatedDate", "UpdatedDate", "PageNumber", "RefNo", "PageId", "ImageUrl", "OemNo") FROM stdin;
\.


--
-- Data for Name: PublicAccessLinks; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."PublicAccessLinks" ("Id", "TokenHash", "UserId", "PublicLinkVersion", "CatalogIds", "ExpiresAtUtc", "IsRevoked", "CreatedDate", "UpdatedDate", "Token") FROM stdin;
\.


--
-- Data for Name: PublicStorefrontViews; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."PublicStorefrontViews" ("Id", "OwnerUserId", "FingerprintHash", "BucketStartUtc", "ViewedAtUtc", "Source", "CreatedDate", "UpdatedDate") FROM stdin;
\.


--
-- Data for Name: StockMovements; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."StockMovements" ("Id", "CreatedDate", "UpdatedDate", "UserId", "ProductId", "ProductCode", "ProductName", "PreviousQuantity", "DeltaQuantity", "NewQuantity", "MovementType", "Reason", "Source", "ActorName", "ReferenceId") FROM stdin;
\.


--
-- Data for Name: UserAiUsageMonthly; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."UserAiUsageMonthly" ("UserId", "MonthStartUtc", "QueryCount", "CreatedDate", "UpdatedDate") FROM stdin;
\.


--
-- Data for Name: Users; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."Users" ("Id", "FirstName", "LastName", "Email", "PasswordHash", "Role", "CompanyName", "CreatedDate", "UpdatedDate", "PublicLinkEnabled", "PublicLinkVersion", "PhoneNumber", "PasswordSalt", "MaxCatalogCount", "MaxPagePerCatalog", "PlanActivatedAt", "PlanExpiresAt", "SubscriptionPlan", "PublicStoreSlug", "IsApproved") FROM stdin;
ec5dabf2-b16f-45e2-952c-6e684beab2ca	Catalog	Smoke	catalog-smoke-1776668119@example.test	vfa2wqQbSrBYlv2+f3tmKzsPKyMWg7sm3FQnykt4V20=	Owner	\N	2026-04-20 06:55:19.581127+00	2026-04-20 06:58:05.328117+00	t	1	\N	3hUKmvvP2q53Pnx87QqGKw==	3	100	\N	\N	1	catalog-smoke	t
\.


--
-- Data for Name: __EFMigrationsHistory; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."__EFMigrationsHistory" ("MigrationId", "ProductVersion") FROM stdin;
20251123122058_InitialCreate	9.0.0
20251123124011_AddCatalogDomain	9.0.0
20251210144736_AddPageNumberToProduct	9.0.0
20251210144942_AddRefNoToProduct	9.0.0
20251218181058_UpdateHotspotForYolo	9.0.0
20260121125247_AddHotspotDimensions	9.0.0
20260122114412_AddAiDescription	9.0.0
20260122183807_AddPageIdToProduct	9.0.0
20260126202051_AddProductNewFields	9.0.0
20260127103131_AddOrderTables	9.0.0
20260128162143_AddCatalogItemsTable	9.0.0
20260130203754_AddFolderStructure	9.0.0
20260131113256_AddFolderTable	9.0.0
20260131130920_AddCatalogItemsRelation	9.0.0
20260131220439_AddVectorSupport	9.0.0
20260131224043_ChangeVectorSizeTo768	9.0.0
20260205231057_AddMachineDetails	9.0.0
20260205235726_AddMachineDetails2	9.0.0
20260206141058_ExpandVectorSize	9.0.0
20260208151655_AddVisualFieldsToCatalogItems	9.0.0
20260208164538_AddCatalogItemVisualImageUrl	9.0.0
20260223190000_AddPublicLinkVersioning	9.0.0
20260224120000_AddCustomers	9.0.0
20260224133000_AddCustomerAuthAndOrderCustomerId	9.0.0
20260224170000_AddCustomerPasswordAuth	9.0.0
20260224174000_AddCustomerLoginSecurity	9.0.0
20260224182000_AddOrderCheckoutFields	9.0.0
20260225190000_ConsolidateRuntimeSchemaUpdates	9.0.0
20260225233000_AddOrderStatusHistory	9.0.0
20260226001000_AddOrderStatusHistoryVisibilityFlag	9.0.0
20260226014500_AddAppUserPasswordSalt	9.0.0
20260226025000_NormalizeAppUserRolesToOwner	9.0.0
20260226040000_AddCatalogViews	9.0.0
20260227021000_AddIsTechnicalDrawingToCatalogPages	9.0.0
20260227183000_AddSubscriptionPlanToUsers	9.0.0
20260227213000_AddUserAiUsageMonthly	9.0.0
20260301110000_AddPublicAccessLinks	9.0.0
20260303201317_AddPlatformAuditLogs	9.0.0
20260304150000_AddPublicStorefrontViews	9.0.0
20260306174000_AddPublicStoreSlugToUsers	9.0.0
20260306193000_AddCatalogPageReviewFields	9.0.0
20260307003000_RepairPublicStoreSlugColumn	9.0.0
20260308213227_AddErpInventorySnapshots	9.0.0
20260310095223_AddEmbedTargets	9.0.0
20260310133457_AddEmbedTargetHostActionsV2	9.0.0
20260313205033_AddEmbedTargetAccessExpiresAt	9.0.0
20260315002208_AddExternalSiteCrawlingSchema	9.0.0
20260315133437_AddManualImportPipeline	9.0.0
20260420075055_AddAppUserApproval	9.0.0
\.


--
-- Name: aggregatedcounter_id_seq; Type: SEQUENCE SET; Schema: hangfire; Owner: postgres
--

SELECT pg_catalog.setval('hangfire.aggregatedcounter_id_seq', 14, true);


--
-- Name: counter_id_seq; Type: SEQUENCE SET; Schema: hangfire; Owner: postgres
--

SELECT pg_catalog.setval('hangfire.counter_id_seq', 10, true);


--
-- Name: hash_id_seq; Type: SEQUENCE SET; Schema: hangfire; Owner: postgres
--

SELECT pg_catalog.setval('hangfire.hash_id_seq', 9, true);


--
-- Name: job_id_seq; Type: SEQUENCE SET; Schema: hangfire; Owner: postgres
--

SELECT pg_catalog.setval('hangfire.job_id_seq', 4, true);


--
-- Name: jobparameter_id_seq; Type: SEQUENCE SET; Schema: hangfire; Owner: postgres
--

SELECT pg_catalog.setval('hangfire.jobparameter_id_seq', 10, true);


--
-- Name: jobqueue_id_seq; Type: SEQUENCE SET; Schema: hangfire; Owner: postgres
--

SELECT pg_catalog.setval('hangfire.jobqueue_id_seq', 24, true);


--
-- Name: list_id_seq; Type: SEQUENCE SET; Schema: hangfire; Owner: postgres
--

SELECT pg_catalog.setval('hangfire.list_id_seq', 1, false);


--
-- Name: set_id_seq; Type: SEQUENCE SET; Schema: hangfire; Owner: postgres
--

SELECT pg_catalog.setval('hangfire.set_id_seq', 43, true);


--
-- Name: state_id_seq; Type: SEQUENCE SET; Schema: hangfire; Owner: postgres
--

SELECT pg_catalog.setval('hangfire.state_id_seq', 92, true);


--
-- Name: aggregatedcounter aggregatedcounter_key_key; Type: CONSTRAINT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.aggregatedcounter
    ADD CONSTRAINT aggregatedcounter_key_key UNIQUE (key);


--
-- Name: aggregatedcounter aggregatedcounter_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.aggregatedcounter
    ADD CONSTRAINT aggregatedcounter_pkey PRIMARY KEY (id);


--
-- Name: counter counter_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.counter
    ADD CONSTRAINT counter_pkey PRIMARY KEY (id);


--
-- Name: hash hash_key_field_key; Type: CONSTRAINT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.hash
    ADD CONSTRAINT hash_key_field_key UNIQUE (key, field);


--
-- Name: hash hash_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.hash
    ADD CONSTRAINT hash_pkey PRIMARY KEY (id);


--
-- Name: job job_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.job
    ADD CONSTRAINT job_pkey PRIMARY KEY (id);


--
-- Name: jobparameter jobparameter_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.jobparameter
    ADD CONSTRAINT jobparameter_pkey PRIMARY KEY (id);


--
-- Name: jobqueue jobqueue_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.jobqueue
    ADD CONSTRAINT jobqueue_pkey PRIMARY KEY (id);


--
-- Name: list list_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.list
    ADD CONSTRAINT list_pkey PRIMARY KEY (id);


--
-- Name: lock lock_resource_key; Type: CONSTRAINT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.lock
    ADD CONSTRAINT lock_resource_key UNIQUE (resource);

ALTER TABLE ONLY hangfire.lock REPLICA IDENTITY USING INDEX lock_resource_key;


--
-- Name: schema schema_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.schema
    ADD CONSTRAINT schema_pkey PRIMARY KEY (version);


--
-- Name: server server_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.server
    ADD CONSTRAINT server_pkey PRIMARY KEY (id);


--
-- Name: set set_key_value_key; Type: CONSTRAINT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.set
    ADD CONSTRAINT set_key_value_key UNIQUE (key, value);


--
-- Name: set set_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.set
    ADD CONSTRAINT set_pkey PRIMARY KEY (id);


--
-- Name: state state_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.state
    ADD CONSTRAINT state_pkey PRIMARY KEY (id);


--
-- Name: CatalogAiJobs PK_CatalogAiJobs; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CatalogAiJobs"
    ADD CONSTRAINT "PK_CatalogAiJobs" PRIMARY KEY ("Id");


--
-- Name: CatalogItemExternalMatches PK_CatalogItemExternalMatches; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CatalogItemExternalMatches"
    ADD CONSTRAINT "PK_CatalogItemExternalMatches" PRIMARY KEY ("Id");


--
-- Name: CatalogItems PK_CatalogItems; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CatalogItems"
    ADD CONSTRAINT "PK_CatalogItems" PRIMARY KEY ("Id");


--
-- Name: CatalogPages PK_CatalogPages; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CatalogPages"
    ADD CONSTRAINT "PK_CatalogPages" PRIMARY KEY ("Id");


--
-- Name: CatalogViews PK_CatalogViews; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CatalogViews"
    ADD CONSTRAINT "PK_CatalogViews" PRIMARY KEY ("Id");


--
-- Name: Catalogs PK_Catalogs; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Catalogs"
    ADD CONSTRAINT "PK_Catalogs" PRIMARY KEY ("Id");


--
-- Name: Customers PK_Customers; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Customers"
    ADD CONSTRAINT "PK_Customers" PRIMARY KEY ("Id");


--
-- Name: EmbedSettings PK_EmbedSettings; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."EmbedSettings"
    ADD CONSTRAINT "PK_EmbedSettings" PRIMARY KEY ("Id");


--
-- Name: EmbedTargets PK_EmbedTargets; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."EmbedTargets"
    ADD CONSTRAINT "PK_EmbedTargets" PRIMARY KEY ("Id");


--
-- Name: ErpInventorySnapshots PK_ErpInventorySnapshots; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."ErpInventorySnapshots"
    ADD CONSTRAINT "PK_ErpInventorySnapshots" PRIMARY KEY ("Id");


--
-- Name: ExternalProductLinkChecks PK_ExternalProductLinkChecks; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."ExternalProductLinkChecks"
    ADD CONSTRAINT "PK_ExternalProductLinkChecks" PRIMARY KEY ("Id");


--
-- Name: ExternalProductOemNumbers PK_ExternalProductOemNumbers; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."ExternalProductOemNumbers"
    ADD CONSTRAINT "PK_ExternalProductOemNumbers" PRIMARY KEY ("Id");


--
-- Name: ExternalProducts PK_ExternalProducts; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."ExternalProducts"
    ADD CONSTRAINT "PK_ExternalProducts" PRIMARY KEY ("Id");


--
-- Name: ExternalSiteCrawls PK_ExternalSiteCrawls; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."ExternalSiteCrawls"
    ADD CONSTRAINT "PK_ExternalSiteCrawls" PRIMARY KEY ("Id");


--
-- Name: ExternalSites PK_ExternalSites; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."ExternalSites"
    ADD CONSTRAINT "PK_ExternalSites" PRIMARY KEY ("Id");


--
-- Name: Folders PK_Folders; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Folders"
    ADD CONSTRAINT "PK_Folders" PRIMARY KEY ("Id");


--
-- Name: Hotspots PK_Hotspots; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Hotspots"
    ADD CONSTRAINT "PK_Hotspots" PRIMARY KEY ("Id");


--
-- Name: ManualImportFiles PK_ManualImportFiles; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."ManualImportFiles"
    ADD CONSTRAINT "PK_ManualImportFiles" PRIMARY KEY ("Id");


--
-- Name: OrderItems PK_OrderItems; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."OrderItems"
    ADD CONSTRAINT "PK_OrderItems" PRIMARY KEY ("Id");


--
-- Name: OrderStatusHistory PK_OrderStatusHistory; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."OrderStatusHistory"
    ADD CONSTRAINT "PK_OrderStatusHistory" PRIMARY KEY ("Id");


--
-- Name: Orders PK_Orders; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Orders"
    ADD CONSTRAINT "PK_Orders" PRIMARY KEY ("Id");


--
-- Name: PlatformAuditLogs PK_PlatformAuditLogs; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."PlatformAuditLogs"
    ADD CONSTRAINT "PK_PlatformAuditLogs" PRIMARY KEY ("Id");


--
-- Name: Products PK_Products; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Products"
    ADD CONSTRAINT "PK_Products" PRIMARY KEY ("Id");


--
-- Name: PublicAccessLinks PK_PublicAccessLinks; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."PublicAccessLinks"
    ADD CONSTRAINT "PK_PublicAccessLinks" PRIMARY KEY ("Id");


--
-- Name: PublicStorefrontViews PK_PublicStorefrontViews; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."PublicStorefrontViews"
    ADD CONSTRAINT "PK_PublicStorefrontViews" PRIMARY KEY ("Id");


--
-- Name: StockMovements PK_StockMovements; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."StockMovements"
    ADD CONSTRAINT "PK_StockMovements" PRIMARY KEY ("Id");


--
-- Name: UserAiUsageMonthly PK_UserAiUsageMonthly; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."UserAiUsageMonthly"
    ADD CONSTRAINT "PK_UserAiUsageMonthly" PRIMARY KEY ("UserId", "MonthStartUtc");


--
-- Name: Users PK_Users; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Users"
    ADD CONSTRAINT "PK_Users" PRIMARY KEY ("Id");


--
-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");


--
-- Name: ix_hangfire_counter_expireat; Type: INDEX; Schema: hangfire; Owner: postgres
--

CREATE INDEX ix_hangfire_counter_expireat ON hangfire.counter USING btree (expireat);


--
-- Name: ix_hangfire_counter_key; Type: INDEX; Schema: hangfire; Owner: postgres
--

CREATE INDEX ix_hangfire_counter_key ON hangfire.counter USING btree (key);


--
-- Name: ix_hangfire_hash_expireat; Type: INDEX; Schema: hangfire; Owner: postgres
--

CREATE INDEX ix_hangfire_hash_expireat ON hangfire.hash USING btree (expireat);


--
-- Name: ix_hangfire_job_expireat; Type: INDEX; Schema: hangfire; Owner: postgres
--

CREATE INDEX ix_hangfire_job_expireat ON hangfire.job USING btree (expireat);


--
-- Name: ix_hangfire_job_statename; Type: INDEX; Schema: hangfire; Owner: postgres
--

CREATE INDEX ix_hangfire_job_statename ON hangfire.job USING btree (statename);


--
-- Name: ix_hangfire_jobparameter_jobidandname; Type: INDEX; Schema: hangfire; Owner: postgres
--

CREATE INDEX ix_hangfire_jobparameter_jobidandname ON hangfire.jobparameter USING btree (jobid, name);


--
-- Name: ix_hangfire_jobqueue_fetchedat_queue_jobid; Type: INDEX; Schema: hangfire; Owner: postgres
--

CREATE INDEX ix_hangfire_jobqueue_fetchedat_queue_jobid ON hangfire.jobqueue USING btree (fetchedat NULLS FIRST, queue, jobid);


--
-- Name: ix_hangfire_jobqueue_jobidandqueue; Type: INDEX; Schema: hangfire; Owner: postgres
--

CREATE INDEX ix_hangfire_jobqueue_jobidandqueue ON hangfire.jobqueue USING btree (jobid, queue);


--
-- Name: ix_hangfire_jobqueue_queueandfetchedat; Type: INDEX; Schema: hangfire; Owner: postgres
--

CREATE INDEX ix_hangfire_jobqueue_queueandfetchedat ON hangfire.jobqueue USING btree (queue, fetchedat);


--
-- Name: ix_hangfire_list_expireat; Type: INDEX; Schema: hangfire; Owner: postgres
--

CREATE INDEX ix_hangfire_list_expireat ON hangfire.list USING btree (expireat);


--
-- Name: ix_hangfire_set_expireat; Type: INDEX; Schema: hangfire; Owner: postgres
--

CREATE INDEX ix_hangfire_set_expireat ON hangfire.set USING btree (expireat);


--
-- Name: ix_hangfire_set_key_score; Type: INDEX; Schema: hangfire; Owner: postgres
--

CREATE INDEX ix_hangfire_set_key_score ON hangfire.set USING btree (key, score);


--
-- Name: ix_hangfire_state_jobid; Type: INDEX; Schema: hangfire; Owner: postgres
--

CREATE INDEX ix_hangfire_state_jobid ON hangfire.state USING btree (jobid);


--
-- Name: IX_CatalogAiJobs_CatalogId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_CatalogAiJobs_CatalogId" ON public."CatalogAiJobs" USING btree ("CatalogId");


--
-- Name: IX_CatalogAiJobs_Status_NextAttemptAt; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_CatalogAiJobs_Status_NextAttemptAt" ON public."CatalogAiJobs" USING btree ("Status", "NextAttemptAt");


--
-- Name: IX_CatalogItemExternalMatches_CatalogId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_CatalogItemExternalMatches_CatalogId" ON public."CatalogItemExternalMatches" USING btree ("CatalogId");


--
-- Name: IX_CatalogItemExternalMatches_CatalogItemId_ConfidenceScore; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_CatalogItemExternalMatches_CatalogItemId_ConfidenceScore" ON public."CatalogItemExternalMatches" USING btree ("CatalogItemId", "ConfidenceScore");


--
-- Name: IX_CatalogItemExternalMatches_CatalogItemId_Status_IsActive; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_CatalogItemExternalMatches_CatalogItemId_Status_IsActive" ON public."CatalogItemExternalMatches" USING btree ("CatalogItemId", "Status", "IsActive") WHERE ((("Status")::text = 'approved'::text) AND ("IsActive" = true));


--
-- Name: IX_CatalogItemExternalMatches_CatalogPageId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_CatalogItemExternalMatches_CatalogPageId" ON public."CatalogItemExternalMatches" USING btree ("CatalogPageId");


--
-- Name: IX_CatalogItemExternalMatches_ExternalProductId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_CatalogItemExternalMatches_ExternalProductId" ON public."CatalogItemExternalMatches" USING btree ("ExternalProductId");


--
-- Name: IX_CatalogItemExternalMatches_ExternalSiteId_Status; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_CatalogItemExternalMatches_ExternalSiteId_Status" ON public."CatalogItemExternalMatches" USING btree ("ExternalSiteId", "Status");


--
-- Name: IX_CatalogItemExternalMatches_ReviewedByUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_CatalogItemExternalMatches_ReviewedByUserId" ON public."CatalogItemExternalMatches" USING btree ("ReviewedByUserId");


--
-- Name: IX_CatalogItems_CatalogId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_CatalogItems_CatalogId" ON public."CatalogItems" USING btree ("CatalogId");


--
-- Name: IX_CatalogPages_CatalogId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_CatalogPages_CatalogId" ON public."CatalogPages" USING btree ("CatalogId");


--
-- Name: IX_CatalogViews_CatalogId_FingerprintHash_BucketStartUtc; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_CatalogViews_CatalogId_FingerprintHash_BucketStartUtc" ON public."CatalogViews" USING btree ("CatalogId", "FingerprintHash", "BucketStartUtc");


--
-- Name: IX_CatalogViews_CatalogId_ViewedAtUtc; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_CatalogViews_CatalogId_ViewedAtUtc" ON public."CatalogViews" USING btree ("CatalogId", "ViewedAtUtc");


--
-- Name: IX_CatalogViews_OwnerUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_CatalogViews_OwnerUserId" ON public."CatalogViews" USING btree ("OwnerUserId");


--
-- Name: IX_Catalogs_FolderId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_Catalogs_FolderId" ON public."Catalogs" USING btree ("FolderId");


--
-- Name: IX_Catalogs_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_Catalogs_UserId" ON public."Catalogs" USING btree ("UserId");


--
-- Name: IX_Customers_UserId_Email; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_Customers_UserId_Email" ON public."Customers" USING btree ("UserId", "Email");


--
-- Name: IX_Customers_UserId_NormalizedPhone; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_Customers_UserId_NormalizedPhone" ON public."Customers" USING btree ("UserId", "NormalizedPhone");


--
-- Name: IX_Customers_UserId_PublicSessionToken; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_Customers_UserId_PublicSessionToken" ON public."Customers" USING btree ("UserId", "PublicSessionToken");


--
-- Name: IX_EmbedSettings_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_EmbedSettings_UserId" ON public."EmbedSettings" USING btree ("UserId");


--
-- Name: IX_EmbedTargets_CatalogId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_EmbedTargets_CatalogId" ON public."EmbedTargets" USING btree ("CatalogId");


--
-- Name: IX_EmbedTargets_CatalogPageId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_EmbedTargets_CatalogPageId" ON public."EmbedTargets" USING btree ("CatalogPageId");


--
-- Name: IX_EmbedTargets_EmbedKey; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_EmbedTargets_EmbedKey" ON public."EmbedTargets" USING btree ("EmbedKey");


--
-- Name: IX_EmbedTargets_UserId_Type_IsActive; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_EmbedTargets_UserId_Type_IsActive" ON public."EmbedTargets" USING btree ("UserId", "Type", "IsActive");


--
-- Name: IX_ErpInventorySnapshots_OwnerUserId_Provider_ExternalProductId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_ErpInventorySnapshots_OwnerUserId_Provider_ExternalProductId" ON public."ErpInventorySnapshots" USING btree ("OwnerUserId", "Provider", "ExternalProductId") WHERE ("ExternalProductId" IS NOT NULL);


--
-- Name: IX_ErpInventorySnapshots_OwnerUserId_Provider_PartCode; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_ErpInventorySnapshots_OwnerUserId_Provider_PartCode" ON public."ErpInventorySnapshots" USING btree ("OwnerUserId", "Provider", "PartCode");


--
-- Name: IX_ErpInventorySnapshots_OwnerUserId_Provider_ProductId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_ErpInventorySnapshots_OwnerUserId_Provider_ProductId" ON public."ErpInventorySnapshots" USING btree ("OwnerUserId", "Provider", "ProductId");


--
-- Name: IX_ErpInventorySnapshots_ProductId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_ErpInventorySnapshots_ProductId" ON public."ErpInventorySnapshots" USING btree ("ProductId");


--
-- Name: IX_ExternalProductLinkChecks_ExternalProductId_CheckedAtUtc; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_ExternalProductLinkChecks_ExternalProductId_CheckedAtUtc" ON public."ExternalProductLinkChecks" USING btree ("ExternalProductId", "CheckedAtUtc");


--
-- Name: IX_ExternalProductOemNumbers_ExternalProductId_NormalizedOemNu~; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_ExternalProductOemNumbers_ExternalProductId_NormalizedOemNu~" ON public."ExternalProductOemNumbers" USING btree ("ExternalProductId", "NormalizedOemNumber");


--
-- Name: IX_ExternalProductOemNumbers_NormalizedOemNumber; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_ExternalProductOemNumbers_NormalizedOemNumber" ON public."ExternalProductOemNumbers" USING btree ("NormalizedOemNumber");


--
-- Name: IX_ExternalProducts_ExternalSiteId_CanonicalUrl; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_ExternalProducts_ExternalSiteId_CanonicalUrl" ON public."ExternalProducts" USING btree ("ExternalSiteId", "CanonicalUrl") WHERE ("CanonicalUrl" IS NOT NULL);


--
-- Name: IX_ExternalProducts_ExternalSiteId_PartCode; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_ExternalProducts_ExternalSiteId_PartCode" ON public."ExternalProducts" USING btree ("ExternalSiteId", "PartCode") WHERE ("PartCode" IS NOT NULL);


--
-- Name: IX_ExternalProducts_ExternalSiteId_Sku; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_ExternalProducts_ExternalSiteId_Sku" ON public."ExternalProducts" USING btree ("ExternalSiteId", "Sku") WHERE ("Sku" IS NOT NULL);


--
-- Name: IX_ExternalProducts_ExternalSiteId_SourceUrl; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_ExternalProducts_ExternalSiteId_SourceUrl" ON public."ExternalProducts" USING btree ("ExternalSiteId", "SourceUrl");


--
-- Name: IX_ExternalProducts_LastSeenInCrawlId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_ExternalProducts_LastSeenInCrawlId" ON public."ExternalProducts" USING btree ("LastSeenInCrawlId");


--
-- Name: IX_ExternalSiteCrawls_ExternalSiteId_CreatedDate; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_ExternalSiteCrawls_ExternalSiteId_CreatedDate" ON public."ExternalSiteCrawls" USING btree ("ExternalSiteId", "CreatedDate");


--
-- Name: IX_ExternalSites_UserId_BaseUrl; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_ExternalSites_UserId_BaseUrl" ON public."ExternalSites" USING btree ("UserId", "BaseUrl");


--
-- Name: IX_Hotspots_PageId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_Hotspots_PageId" ON public."Hotspots" USING btree ("PageId");


--
-- Name: IX_Hotspots_ProductId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_Hotspots_ProductId" ON public."Hotspots" USING btree ("ProductId");


--
-- Name: IX_ManualImportFiles_ExternalSiteId_ImportedAtUtc; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_ManualImportFiles_ExternalSiteId_ImportedAtUtc" ON public."ManualImportFiles" USING btree ("ExternalSiteId", "ImportedAtUtc");


--
-- Name: IX_ManualImportFiles_ImportedByUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_ManualImportFiles_ImportedByUserId" ON public."ManualImportFiles" USING btree ("ImportedByUserId");


--
-- Name: IX_OrderItems_OrderId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_OrderItems_OrderId" ON public."OrderItems" USING btree ("OrderId");


--
-- Name: IX_OrderItems_ProductId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_OrderItems_ProductId" ON public."OrderItems" USING btree ("ProductId");


--
-- Name: IX_OrderStatusHistory_OrderId_CreatedDate; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_OrderStatusHistory_OrderId_CreatedDate" ON public."OrderStatusHistory" USING btree ("OrderId", "CreatedDate");


--
-- Name: IX_Orders_CustomerId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_Orders_CustomerId" ON public."Orders" USING btree ("CustomerId");


--
-- Name: IX_Orders_IdempotencyKey; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_Orders_IdempotencyKey" ON public."Orders" USING btree ("IdempotencyKey") WHERE (("IdempotencyKey" IS NOT NULL) AND (("IdempotencyKey")::text <> ''::text));


--
-- Name: IX_Orders_OwnerUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_Orders_OwnerUserId" ON public."Orders" USING btree ("OwnerUserId");


--
-- Name: IX_PlatformAuditLogs_ActorUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_PlatformAuditLogs_ActorUserId" ON public."PlatformAuditLogs" USING btree ("ActorUserId");


--
-- Name: IX_PlatformAuditLogs_CreatedDate; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_PlatformAuditLogs_CreatedDate" ON public."PlatformAuditLogs" USING btree ("CreatedDate");


--
-- Name: IX_PlatformAuditLogs_TargetOwnerUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_PlatformAuditLogs_TargetOwnerUserId" ON public."PlatformAuditLogs" USING btree ("TargetOwnerUserId");


--
-- Name: IX_Products_CatalogId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_Products_CatalogId" ON public."Products" USING btree ("CatalogId");


--
-- Name: IX_PublicAccessLinks_Token; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_PublicAccessLinks_Token" ON public."PublicAccessLinks" USING btree ("Token") WHERE ("Token" IS NOT NULL);


--
-- Name: IX_PublicAccessLinks_TokenHash; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_PublicAccessLinks_TokenHash" ON public."PublicAccessLinks" USING btree ("TokenHash");


--
-- Name: IX_PublicAccessLinks_UserId_ExpiresAtUtc; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_PublicAccessLinks_UserId_ExpiresAtUtc" ON public."PublicAccessLinks" USING btree ("UserId", "ExpiresAtUtc");


--
-- Name: IX_PublicStorefrontViews_OwnerUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_PublicStorefrontViews_OwnerUserId" ON public."PublicStorefrontViews" USING btree ("OwnerUserId");


--
-- Name: IX_PublicStorefrontViews_OwnerUserId_FingerprintHash_BucketStar; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_PublicStorefrontViews_OwnerUserId_FingerprintHash_BucketStar" ON public."PublicStorefrontViews" USING btree ("OwnerUserId", "FingerprintHash", "BucketStartUtc");


--
-- Name: IX_PublicStorefrontViews_OwnerUserId_ViewedAtUtc; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_PublicStorefrontViews_OwnerUserId_ViewedAtUtc" ON public."PublicStorefrontViews" USING btree ("OwnerUserId", "ViewedAtUtc");


--
-- Name: IX_StockMovements_ProductId_CreatedDate; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_StockMovements_ProductId_CreatedDate" ON public."StockMovements" USING btree ("ProductId", "CreatedDate");


--
-- Name: IX_StockMovements_UserId_CreatedDate; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_StockMovements_UserId_CreatedDate" ON public."StockMovements" USING btree ("UserId", "CreatedDate");


--
-- Name: IX_UserAiUsageMonthly_MonthStartUtc; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_UserAiUsageMonthly_MonthStartUtc" ON public."UserAiUsageMonthly" USING btree ("MonthStartUtc");


--
-- Name: IX_Users_PublicStoreSlug; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_Users_PublicStoreSlug" ON public."Users" USING btree ("PublicStoreSlug") WHERE ("PublicStoreSlug" IS NOT NULL);


--
-- Name: jobparameter jobparameter_jobid_fkey; Type: FK CONSTRAINT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.jobparameter
    ADD CONSTRAINT jobparameter_jobid_fkey FOREIGN KEY (jobid) REFERENCES hangfire.job(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: state state_jobid_fkey; Type: FK CONSTRAINT; Schema: hangfire; Owner: postgres
--

ALTER TABLE ONLY hangfire.state
    ADD CONSTRAINT state_jobid_fkey FOREIGN KEY (jobid) REFERENCES hangfire.job(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: CatalogAiJobs FK_CatalogAiJobs_Catalogs_CatalogId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CatalogAiJobs"
    ADD CONSTRAINT "FK_CatalogAiJobs_Catalogs_CatalogId" FOREIGN KEY ("CatalogId") REFERENCES public."Catalogs"("Id") ON DELETE CASCADE;


--
-- Name: CatalogItemExternalMatches FK_CatalogItemExternalMatches_CatalogItems_CatalogItemId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CatalogItemExternalMatches"
    ADD CONSTRAINT "FK_CatalogItemExternalMatches_CatalogItems_CatalogItemId" FOREIGN KEY ("CatalogItemId") REFERENCES public."CatalogItems"("Id") ON DELETE CASCADE;


--
-- Name: CatalogItemExternalMatches FK_CatalogItemExternalMatches_CatalogPages_CatalogPageId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CatalogItemExternalMatches"
    ADD CONSTRAINT "FK_CatalogItemExternalMatches_CatalogPages_CatalogPageId" FOREIGN KEY ("CatalogPageId") REFERENCES public."CatalogPages"("Id");


--
-- Name: CatalogItemExternalMatches FK_CatalogItemExternalMatches_Catalogs_CatalogId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CatalogItemExternalMatches"
    ADD CONSTRAINT "FK_CatalogItemExternalMatches_Catalogs_CatalogId" FOREIGN KEY ("CatalogId") REFERENCES public."Catalogs"("Id") ON DELETE CASCADE;


--
-- Name: CatalogItemExternalMatches FK_CatalogItemExternalMatches_ExternalProducts_ExternalProduct~; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CatalogItemExternalMatches"
    ADD CONSTRAINT "FK_CatalogItemExternalMatches_ExternalProducts_ExternalProduct~" FOREIGN KEY ("ExternalProductId") REFERENCES public."ExternalProducts"("Id");


--
-- Name: CatalogItemExternalMatches FK_CatalogItemExternalMatches_ExternalSites_ExternalSiteId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CatalogItemExternalMatches"
    ADD CONSTRAINT "FK_CatalogItemExternalMatches_ExternalSites_ExternalSiteId" FOREIGN KEY ("ExternalSiteId") REFERENCES public."ExternalSites"("Id") ON DELETE CASCADE;


--
-- Name: CatalogItemExternalMatches FK_CatalogItemExternalMatches_Users_ReviewedByUserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CatalogItemExternalMatches"
    ADD CONSTRAINT "FK_CatalogItemExternalMatches_Users_ReviewedByUserId" FOREIGN KEY ("ReviewedByUserId") REFERENCES public."Users"("Id");


--
-- Name: CatalogItems FK_CatalogItems_Catalogs_CatalogId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CatalogItems"
    ADD CONSTRAINT "FK_CatalogItems_Catalogs_CatalogId" FOREIGN KEY ("CatalogId") REFERENCES public."Catalogs"("Id") ON DELETE CASCADE;


--
-- Name: CatalogPages FK_CatalogPages_Catalogs_CatalogId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CatalogPages"
    ADD CONSTRAINT "FK_CatalogPages_Catalogs_CatalogId" FOREIGN KEY ("CatalogId") REFERENCES public."Catalogs"("Id") ON DELETE CASCADE;


--
-- Name: CatalogViews FK_CatalogViews_Catalogs_CatalogId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CatalogViews"
    ADD CONSTRAINT "FK_CatalogViews_Catalogs_CatalogId" FOREIGN KEY ("CatalogId") REFERENCES public."Catalogs"("Id") ON DELETE CASCADE;


--
-- Name: Catalogs FK_Catalogs_Folders_FolderId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Catalogs"
    ADD CONSTRAINT "FK_Catalogs_Folders_FolderId" FOREIGN KEY ("FolderId") REFERENCES public."Folders"("Id");


--
-- Name: Catalogs FK_Catalogs_Users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Catalogs"
    ADD CONSTRAINT "FK_Catalogs_Users_UserId" FOREIGN KEY ("UserId") REFERENCES public."Users"("Id") ON DELETE CASCADE;


--
-- Name: EmbedSettings FK_EmbedSettings_Users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."EmbedSettings"
    ADD CONSTRAINT "FK_EmbedSettings_Users_UserId" FOREIGN KEY ("UserId") REFERENCES public."Users"("Id") ON DELETE CASCADE;


--
-- Name: EmbedTargets FK_EmbedTargets_CatalogPages_CatalogPageId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."EmbedTargets"
    ADD CONSTRAINT "FK_EmbedTargets_CatalogPages_CatalogPageId" FOREIGN KEY ("CatalogPageId") REFERENCES public."CatalogPages"("Id") ON DELETE SET NULL;


--
-- Name: EmbedTargets FK_EmbedTargets_Catalogs_CatalogId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."EmbedTargets"
    ADD CONSTRAINT "FK_EmbedTargets_Catalogs_CatalogId" FOREIGN KEY ("CatalogId") REFERENCES public."Catalogs"("Id") ON DELETE CASCADE;


--
-- Name: EmbedTargets FK_EmbedTargets_Users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."EmbedTargets"
    ADD CONSTRAINT "FK_EmbedTargets_Users_UserId" FOREIGN KEY ("UserId") REFERENCES public."Users"("Id") ON DELETE CASCADE;


--
-- Name: ErpInventorySnapshots FK_ErpInventorySnapshots_Products_ProductId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."ErpInventorySnapshots"
    ADD CONSTRAINT "FK_ErpInventorySnapshots_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES public."Products"("Id") ON DELETE SET NULL;


--
-- Name: ExternalProductLinkChecks FK_ExternalProductLinkChecks_ExternalProducts_ExternalProductId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."ExternalProductLinkChecks"
    ADD CONSTRAINT "FK_ExternalProductLinkChecks_ExternalProducts_ExternalProductId" FOREIGN KEY ("ExternalProductId") REFERENCES public."ExternalProducts"("Id") ON DELETE CASCADE;


--
-- Name: ExternalProductOemNumbers FK_ExternalProductOemNumbers_ExternalProducts_ExternalProductId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."ExternalProductOemNumbers"
    ADD CONSTRAINT "FK_ExternalProductOemNumbers_ExternalProducts_ExternalProductId" FOREIGN KEY ("ExternalProductId") REFERENCES public."ExternalProducts"("Id") ON DELETE CASCADE;


--
-- Name: ExternalProducts FK_ExternalProducts_ExternalSiteCrawls_LastSeenInCrawlId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."ExternalProducts"
    ADD CONSTRAINT "FK_ExternalProducts_ExternalSiteCrawls_LastSeenInCrawlId" FOREIGN KEY ("LastSeenInCrawlId") REFERENCES public."ExternalSiteCrawls"("Id");


--
-- Name: ExternalProducts FK_ExternalProducts_ExternalSites_ExternalSiteId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."ExternalProducts"
    ADD CONSTRAINT "FK_ExternalProducts_ExternalSites_ExternalSiteId" FOREIGN KEY ("ExternalSiteId") REFERENCES public."ExternalSites"("Id") ON DELETE CASCADE;


--
-- Name: ExternalSiteCrawls FK_ExternalSiteCrawls_ExternalSites_ExternalSiteId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."ExternalSiteCrawls"
    ADD CONSTRAINT "FK_ExternalSiteCrawls_ExternalSites_ExternalSiteId" FOREIGN KEY ("ExternalSiteId") REFERENCES public."ExternalSites"("Id") ON DELETE CASCADE;


--
-- Name: ExternalSites FK_ExternalSites_Users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."ExternalSites"
    ADD CONSTRAINT "FK_ExternalSites_Users_UserId" FOREIGN KEY ("UserId") REFERENCES public."Users"("Id") ON DELETE CASCADE;


--
-- Name: Hotspots FK_Hotspots_CatalogPages_PageId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Hotspots"
    ADD CONSTRAINT "FK_Hotspots_CatalogPages_PageId" FOREIGN KEY ("PageId") REFERENCES public."CatalogPages"("Id") ON DELETE CASCADE;


--
-- Name: Hotspots FK_Hotspots_Products_ProductId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Hotspots"
    ADD CONSTRAINT "FK_Hotspots_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES public."Products"("Id");


--
-- Name: ManualImportFiles FK_ManualImportFiles_ExternalSites_ExternalSiteId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."ManualImportFiles"
    ADD CONSTRAINT "FK_ManualImportFiles_ExternalSites_ExternalSiteId" FOREIGN KEY ("ExternalSiteId") REFERENCES public."ExternalSites"("Id") ON DELETE CASCADE;


--
-- Name: ManualImportFiles FK_ManualImportFiles_Users_ImportedByUserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."ManualImportFiles"
    ADD CONSTRAINT "FK_ManualImportFiles_Users_ImportedByUserId" FOREIGN KEY ("ImportedByUserId") REFERENCES public."Users"("Id") ON DELETE CASCADE;


--
-- Name: OrderItems FK_OrderItems_Orders_OrderId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."OrderItems"
    ADD CONSTRAINT "FK_OrderItems_Orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES public."Orders"("Id") ON DELETE CASCADE;


--
-- Name: OrderItems FK_OrderItems_Products_ProductId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."OrderItems"
    ADD CONSTRAINT "FK_OrderItems_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES public."Products"("Id") ON DELETE RESTRICT;


--
-- Name: OrderStatusHistory FK_OrderStatusHistory_Orders_OrderId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."OrderStatusHistory"
    ADD CONSTRAINT "FK_OrderStatusHistory_Orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES public."Orders"("Id") ON DELETE CASCADE;


--
-- Name: Products FK_Products_Catalogs_CatalogId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Products"
    ADD CONSTRAINT "FK_Products_Catalogs_CatalogId" FOREIGN KEY ("CatalogId") REFERENCES public."Catalogs"("Id") ON DELETE CASCADE;


--
-- Name: PublicAccessLinks FK_PublicAccessLinks_Users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."PublicAccessLinks"
    ADD CONSTRAINT "FK_PublicAccessLinks_Users_UserId" FOREIGN KEY ("UserId") REFERENCES public."Users"("Id") ON DELETE CASCADE;


--
-- Name: StockMovements FK_StockMovements_Products_ProductId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."StockMovements"
    ADD CONSTRAINT "FK_StockMovements_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES public."Products"("Id") ON DELETE CASCADE;


--
-- Name: UserAiUsageMonthly FK_UserAiUsageMonthly_Users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."UserAiUsageMonthly"
    ADD CONSTRAINT "FK_UserAiUsageMonthly_Users_UserId" FOREIGN KEY ("UserId") REFERENCES public."Users"("Id") ON DELETE CASCADE;


--
-- PostgreSQL database dump complete
--

\unrestrict z4dHnhboAyzSORpbu7wX19THTm47qFRD7cUFBxnIKfhZdyTQmomSdvp1Fae6fr4
