To run the Liquibase scripts locally and test your changes, you have two main options: using **Docker** (recommended as it handles all dependencies) or using the **Liquibase CLI**.

I have updated the `compose.yml` and `database/liquibase.properties` to ensure they are correctly configured for your local environment.

### Option 1: Using Docker (Recommended)
This is the easiest way as it uses the official Liquibase Docker image and connects to the Postgres container defined in your `compose.yml`.

1.  **Start the database:**
    ```bash
    docker compose up -d postgres
    ```

2.  **Run the Liquibase update:**
    ```bash
    docker compose up liquibase
    ```
    *Note: The `liquibase` service in `compose.yml` is configured to wait for the database to be healthy and then apply the changes from `database/db.changelog-master.xml`.*

### Option 2: Using Liquibase CLI
If you have Liquibase installed locally on your machine, you can run it directly from the `database` folder.

1.  **Navigate to the database directory:**
    ```bash
    cd database
    ```

2.  **Run the update command:**
    ```bash
    liquibase update
    ```
    *This command will automatically use the settings defined in `liquibase.properties` (URL, username, password, and the `db.changelog-master.xml` file).*

### Verifying the Changes
Once the command finishes successfully, you can verify that the schema and data have been created:

1.  **Connect to the database using `psql` (via Docker):**
    ```bash
    docker exec -it $(docker ps -q -f name=postgres) psql -U postgres -d livestock_auth
    ```

2.  **Check the tables in the `defra-ci` schema:**
    ```sql
    SET search_path TO "defra-ci";
    \dt
    SELECT * FROM status_type;
    ```

### Summary of Configuration Fixes:
-   **`compose.yml`**: Updated the `liquibase` service to mount the `./database` directory and corrected the connection URL to use the container name `postgres` and the correct `currentSchema=defra-ci`.
-   **`database/liquibase.properties`**: Corrected `changeLogFile` to point to `db.changelog-master.xml` instead of the raw SQL file.