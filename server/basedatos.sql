DROP DATABASE IF EXISTS duska_project;

CREATE DATABASE duska_project;

USE duska_project;

CREATE TABLE Jugadores (
    id_jugador INT AUTO_INCREMENT PRIMARY KEY,
    nombre_usuario VARCHAR(50) UNIQUE NOT NULL,
    contrasena VARCHAR(255) NOT NULL,
    fecha_registro DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Partidas (
    id_partida INT AUTO_INCREMENT PRIMARY KEY,
    fecha_inicio DATETIME DEFAULT CURRENT_TIMESTAMP,
    duracion INT NOT NULL,
    ganador_id INT NOT NULL,
    jugadores_ids JSON NOT NULL,
    FOREIGN KEY (ganador_id) REFERENCES Jugadores(id_jugador)
);