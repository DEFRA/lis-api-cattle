alter table public.submissions
    drop constraint if exists submission_status_check;

alter table public.submissions
    add constraint submission_status_check
        check (status = ANY (ARRAY ['pending'::text, 'submitted'::text, 'processing'::text, 'complete'::text, 'error'::text]));

alter table public.submission_animals
    drop constraint if exists submission_animal_status_check;

alter table public.submission_animals
    add constraint submission_animal_status_check
        check (status = ANY (ARRAY ['pending'::text, 'submitted'::text, 'processing'::text, 'complete'::text, 'error'::text]));
