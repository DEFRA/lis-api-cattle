-- LREG-925: bundles are submitted to CTS asynchronously. The bundle records the CTS outcome (receipt or error),
-- and the CTS request payload is stored before it is sent.

alter table public.submissions
    add column if not exists correlation_id     text,
    add column if not exists cts_receipt_number bigint,
    add column if not exists cts_sent_at        timestamp with time zone,
    add column if not exists error_code         text,
    add column if not exists error_text         text,
    add column if not exists updated_at         timestamp with time zone;

create table if not exists public.cts_requests
(
    id             uuid                     default uuid_generate_v4() not null
        constraint cts_request_pk
            primary key,
    submission_id  uuid                                                not null
        constraint cts_request_submission_id_fk
            references public.submissions
        constraint cts_request_submission_id_unique
            unique,
    txn_id         text                                                not null
        constraint cts_request_txn_id_unique
            unique,
    operation      text                                                not null,
    payload        text                                                not null,
    created_at     timestamp with time zone default now()              not null,
    sent_at        timestamp with time zone,
    receipt_number bigint,
    last_error     text
);

alter table public.cts_requests
    owner to lis_api_cattle_ddl;
