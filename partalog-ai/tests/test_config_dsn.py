import unittest

from config import _asyncpg_connect_kwargs, _normalize_db_dsn


class NormalizeDbDsnTests(unittest.TestCase):
    def test_keeps_postgresql_uri_and_normalizes_scheme(self):
        self.assertEqual(
            "postgresql://user:pass@localhost/db",
            _normalize_db_dsn("postgresql+asyncpg://user:pass@localhost/db"),
        )

    def test_converts_npgsql_cloud_sql_socket_connection(self):
        actual = _normalize_db_dsn(
            "Host=/cloudsql/partalog:europe-west1:katalogcu-db;"
            "Database=KatalogcuDb;Username=katalogcu_app;Password=p@ss word"
        )

        self.assertEqual(
            "postgresql://katalogcu_app:p%40ss%20word@/KatalogcuDb?"
            "host=%2Fcloudsql%2Fpartalog%3Aeurope-west1%3Akatalogcu-db",
            actual,
        )

    def test_builds_explicit_asyncpg_kwargs_for_cloud_sql_socket(self):
        actual = _asyncpg_connect_kwargs(
            "Host=/cloudsql/partalog:europe-west1:katalogcu-db;Port=5432;"
            "Database=PartalogDb;Username=partalog_app;Password=p@ss word"
        )

        self.assertEqual(
            {
                "host": "/cloudsql/partalog:europe-west1:katalogcu-db",
                "port": 5432,
                "database": "PartalogDb",
                "user": "partalog_app",
                "password": "p@ss word",
            },
            actual,
        )


if __name__ == "__main__":
    unittest.main()
