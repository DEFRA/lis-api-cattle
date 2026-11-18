-- LREG-926: the CTS polling job fetches each sent bundle's validation results by receipt and completes the bundle
-- once every animal has its final outcome. A bundle where CTS rejected some animals completes as
-- 'complete_with_errors', with the failures recorded per animal.

alter table public.submissions
    add column if not exists completed_at       timestamp with time zone,
    add column if not exists cts_last_polled_at timestamp with time zone;

alter table public.submissions
    drop constraint if exists submission_status_check;

alter table public.submissions
    add constraint submission_status_check
        check (status = ANY (ARRAY ['pending'::text, 'submitted'::text, 'processing'::text, 'complete'::text, 'complete_with_errors'::text, 'error'::text]));

-- The poller claims sent, uncompleted bundles, longest since their last poll first.
create index if not exists submission_awaiting_cts_results_index
    on public.submissions (cts_last_polled_at nulls first, created_at)
    where completed_at is null and cts_receipt_number is not null;
