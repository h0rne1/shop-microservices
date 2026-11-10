-- Выполняется PostgreSQL только при первом создании volume db_data.
-- Каждый сервис владеет своей базой данных (database-per-service).
CREATE DATABASE catalog_db;
CREATE DATABASE orders_db;
CREATE DATABASE payments_db;
