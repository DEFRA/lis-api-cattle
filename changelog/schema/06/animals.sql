-- LREG-1003: the LIS master record for each animal, created in the 'pending' state when the registration bundle
-- that names it is received, and linked to the submission and submission animal that created it. An ear tag is
-- never issued to two animals, so it identifies the record across every submission. The errors CTS reports for an
-- animal are recorded against it with the same CTWS codes and text as the API's submission validation.

create table if not exists public.animals
(
    id                    uuid                     default uuid_generate_v4() not null
        constraint animal_pk
            primary key,
    ear_tag               text                                                not null
        constraint animal_ear_tag_unique
            unique,
    status                text                     default 'pending'::text    not null
        constraint animal_status_check
            check (status = ANY (ARRAY ['pending'::text, 'registered'::text, 'error'::text])),
    date_birth            date,
    sex                   text,
    breed                 text,
    dam_type              text,
    dam_genetic_ear_tag   text,
    dam_surrogate_ear_tag text,
    sire_ear_tag          text,
    sire_name             text,
    submission_id         uuid                                                not null
        constraint animal_submission_id_fk
            references public.submissions,
    submission_animal_id  uuid                                                not null
        constraint animal_submission_animal_id_fk
            references public.submission_animals
        constraint animal_submission_animal_id_unique
            unique,
    created_at            timestamp with time zone default now()              not null,
    updated_at            timestamp with time zone
);

alter table public.animals
    owner to lis_api_cattle_ddl;

create index if not exists animal_submission_id_index
    on public.animals (submission_id);

create table if not exists public.animal_errors
(
    id         uuid                     default uuid_generate_v4() not null
        constraint animal_error_pk
            primary key,
    animal_id  uuid                                                not null
        constraint animal_error_animal_id_fk
            references public.animals,
    error_code text                                                not null,
    error_text text                                                not null,
    source     text                                                not null
        constraint animal_error_source_check
            check (source = ANY (ARRAY ['cts'::text, 'api'::text])),
    created_at timestamp with time zone default now()              not null,
    constraint animal_error_animal_id_error_code_unique
        unique (animal_id, error_code)
);

alter table public.animal_errors
    owner to lis_api_cattle_ddl;
