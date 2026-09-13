CREATE TABLE battles (
    id uuid PRIMARY KEY,
    version integer NOT NULL CHECK (version > 0),
    document jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT battles_document_identity CHECK (
        document->>'id' IS NOT NULL AND (document->>'id')::uuid = id),
    CONSTRAINT battles_document_version CHECK (
        document->>'version' IS NOT NULL AND (document->>'version')::integer = version),
    CONSTRAINT battles_document_format CHECK (
        document->>'formatVersion' IS NOT NULL AND (document->>'formatVersion')::integer = 1)
);
