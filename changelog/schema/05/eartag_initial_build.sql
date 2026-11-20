--liquibase formatted sql

--changeset cedric.brasey:eartag-001-schema
--comment: eartag schema
create schema if not exists eartag;
--rollback drop schema eartag;

--changeset cedric.brasey:eartag-002-lookups
--comment: status and order_type lookups. Ids are assigned explicitly so they match in every environment.
create table eartag.status
(
  id          smallint          not null,
  code        character varying not null,
  description character varying,
  constraint status_pk primary key (id),
  constraint status_code_unique unique (code)
);

insert into eartag.status (id, code, description)
values (1, 'NEW', 'New'),
       (2, 'CANCEL', 'Cancel'),
       (3, 'REINSTATEMENT', 'Re-instatement'),
       (4, 'REPLICA', 'Replica');

create table eartag.order_type
(
  id          smallint          not null,
  code        character varying not null,
  description character varying,
  constraint order_type_pk primary key (id),
  constraint order_type_code_unique unique (code)
);

insert into eartag.order_type (id, code, description)
values (1, 'CONVENTIONAL', 'Conventional'),
       (2, 'EID', 'EID'),
       (3, 'REINSTATEMENT', 'Reinstatement'),
       (4, 'REPLICA', 'Replica'),
       (5, 'SLAUGHTER', 'Slaughter');
--rollback drop table eartag.order_type;
--rollback drop table eartag.status;

--changeset cedric.brasey:eartag-003-manufacturer
create table eartag.manufacturer
(
  id          uuid              not null default uuidv7(),
  external_id character varying not null,
  name        character varying not null,
  constraint manufacturer_pk primary key (id)
);

create index manufacturer_external_id_index on eartag.manufacturer (external_id);
create index manufacturer_name_index on eartag.manufacturer (name);
--rollback drop table eartag.manufacturer;

--changeset cedric.brasey:eartag-004-tag-specification
create table eartag.tag_specification
(
  id                uuid              not null default uuidv7(),
  external_id       character varying not null,
  is_eid            boolean           not null,
  is_primary        boolean           not null,
  is_slaughter      boolean,
  legacy_code       character varying,
  manufactruer_code character varying,
  tag_class         character varying not null,
  constraint tag_specification_pk primary key (id)
);

create index tag_specification_external_id_index on eartag.tag_specification (external_id);
create index tag_specification_is_eid_index on eartag.tag_specification (is_eid);
create index tag_specification_is_primary_index on eartag.tag_specification (is_primary);
create index tag_specification_is_slaughter_index on eartag.tag_specification (is_slaughter);
create index tag_specification_tag_class_index on eartag.tag_specification (tag_class);
--rollback drop table eartag.tag_specification;

--changeset cedric.brasey:eartag-005-eartag
create table eartag.eartag
(
  id                   uuid                     not null default uuidv7(),
  tag                  character varying        not null,
  status               smallint                 not null,
  country              integer,
  cph                  character varying        not null,
  herd                 character varying,
  for_tag              integer,
  order_type           smallint,
  order_date           timestamp with time zone,
  defra_ref            character varying,
  colour               integer,
  manufacturer_id      uuid,
  tag_specification_id uuid,
  constraint eartag_pk primary key (id),
  constraint eartag_tag_unique unique (tag),
  constraint eartag_cph_format_check check (cph ~ '^[0-9]{2}/[0-9]{3}/[0-9]{4}$'),
    constraint eartag_status_fk foreign key (status) references eartag.status (id),
    constraint eartag_order_type_fk foreign key (order_type) references eartag.order_type (id),
    constraint eartag_manufacturer_id_fk foreign key (manufacturer_id) references eartag.manufacturer (id),
    constraint eartag_tag_specification_id_fk foreign key (tag_specification_id) references eartag.tag_specification (id)
);

-- Serves: select tag from eartag where cph = ? and status = ? order by tag (index-only scan).
-- Also covers cph-only lookups.
create index eartag_cph_status_tag_index on eartag.eartag (cph, status, tag);
--rollback drop table eartag.eartag;

--changeset cedric.brasey:eartag-006-eartag-history
--comment: one row per status transition; written only by trigger
create table eartag.eartag_history
(
  id              uuid                     not null default uuidv7(),
  eartag_id       uuid                     not null,
  previous_status smallint,
  status          smallint                 not null,
  created_at      timestamp with time zone not null default clock_timestamp(),
  constraint eartag_history_pk primary key (id),
  constraint eartag_history_eartag_id_fk foreign key (eartag_id) references eartag.eartag (id) on delete cascade,
  constraint eartag_history_status_fk foreign key (status) references eartag.status (id),
  constraint eartag_history_previous_status_fk foreign key (previous_status) references eartag.status (id)
);

create index eartag_history_eartag_id_created_at_index on eartag.eartag_history (eartag_id, created_at);
--rollback drop table eartag.eartag_history;

--changeset cedric.brasey:eartag-007-status-transition-trigger splitStatements:false
--comment: record a history row when an eartag is created or its status changes
create function eartag.record_status_transition() returns trigger
  language plpgsql as
$$
begin
insert into eartag.eartag_history (eartag_id, previous_status, status)
values (new.id, case when tg_op = 'UPDATE' then old.status end, new.status);
return null;
end;
$$;

create trigger eartag_status_insert_history
  after insert on eartag.eartag
  for each row execute function eartag.record_status_transition();

create trigger eartag_status_update_history
  after update of status on eartag.eartag
  for each row when (old.status is distinct from new.status)
    execute function eartag.record_status_transition();
--rollback drop trigger eartag_status_update_history on eartag.eartag;
--rollback drop trigger eartag_status_insert_history on eartag.eartag;
--rollback drop function eartag.record_status_transition();

--changeset cedric.brasey:eartag-008-history-write-guard splitStatements:false
--comment: history is written only by the transition trigger, so eartag.status always matches the latest entry
create function eartag.prevent_direct_history_write() returns trigger
  language plpgsql as
$$
begin
    if pg_trigger_depth() < 2 then
        raise exception 'eartag_history is maintained automatically; change eartag.status instead';
end if;
return new;
end;
$$;

create trigger eartag_history_no_direct_write
  before insert or update on eartag.eartag_history
                     for each row execute function eartag.prevent_direct_history_write();
--rollback drop trigger eartag_history_no_direct_write on eartag.eartag_history;
--rollback drop function eartag.prevent_direct_history_write();
