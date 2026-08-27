CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

create table public.submissions
(
    id                    uuid                     default uuid_generate_v4() not null
        constraint submission_pk
            primary key,
    client_reference      text                                                not null,
    county_parish_holding text                                                not null,
    submitted_by          text                                                not null,
    status                text                     default 'submitted'::text  not null
        constraint submission_status_check
            check (status = ANY (ARRAY ['pending'::text, 'submitted'::text, 'processing'::text, 'complete'::text, 'error'::text])),
    created_at            timestamp with time zone default now()              not null
);

alter table public.submissions
    owner to lis_api_cattle_ddl;

create index submission_client_reference_index
    on public.submissions (client_reference);

create index submission_county_parish_holding_index
    on public.submissions (county_parish_holding);

create table public.submission_animals
(
    id                    uuid default uuid_generate_v4() not null
        constraint submission_animal_pk
            primary key,
    submission_id         uuid                            not null
        constraint submission_animal_submission_id_fk
            references public.submissions,
    status                text                            not null
        constraint submission_animal_status_check
            check (status = ANY (ARRAY ['pending'::text, 'submitted'::text, 'processing'::text, 'complete'::text, 'error'::text])),
    ear_tag               text                            not null,
    date_birth            date,
    sex                   text,
    breed                 text,
    dam_type              text,
    dam_genetic_ear_tag   text,
    dam_surrogate_ear_tag text,
    sire_ear_tag          text,
    sire_name             text
);

alter table public.submission_animals
    owner to lis_api_cattle_ddl;

create table public.submission_animal_errors
(
    id         uuid                     default uuid_generate_v4() not null
        constraint submission_animal_errors_pk
            primary key,
    animal_id  uuid                                                not null
        constraint submission_animal_errors_submission_animal_id_fk
            references public.submission_animals,
    error_code text                                                not null,
    error_text text                                                not null,
    created_at timestamp with time zone default now(),
    created_by text,
    updated_at timestamp with time zone,
    updated_by text
);

alter table public.submission_animal_errors
    owner to lis_api_cattle_ddl;

