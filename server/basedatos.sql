DROP DATABASE IF EXISTS duska_project;

CREATE DATABASE duska_project;

USE duska_project;

CREATE TABLE IF NOT EXISTS Jugadores (
    id_jugador INT AUTO_INCREMENT PRIMARY KEY,
    nombre_usuario VARCHAR(50) UNIQUE NOT NULL,
    contrasena VARCHAR(255) NOT NULL,
    fecha_registro DATETIME DEFAULT CURRENT_TIMESTAMP,
    ultima_conexion DATETIME DEFAULT CURRENT_TIMESTAMP,
    estado INT DEFAULT 0
);

CREATE TABLE IF NOT EXISTS Partidas (
    id_partida INT AUTO_INCREMENT PRIMARY KEY,
    fecha_inicio TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    fecha_fin TIMESTAMP NULL,
    estado VARCHAR(20) DEFAULT 'ACTIVA',
    ganador_id INT NULL,
    num_jugadores INT NOT NULL DEFAULT 2,
    jugadores TEXT NULL,
    FOREIGN KEY (ganador_id) REFERENCES Jugadores(id_jugador)
);

CREATE TABLE IF NOT EXISTS Partida_Jugadores (
    id INT AUTO_INCREMENT PRIMARY KEY,
    id_partida INT NOT NULL,
    id_jugador INT NOT NULL,
    nombre_usuario VARCHAR(50) NOT NULL,
    posicion_turno INT NOT NULL,
    FOREIGN KEY (id_partida) REFERENCES Partidas(id_partida),
    FOREIGN KEY (id_jugador) REFERENCES Jugadores(id_jugador)
);

CREATE TABLE IF NOT EXISTS Participantes (
    id_partida INT NOT NULL,
    id_jugador INT NOT NULL,
    fecha_participacion DATETIME DEFAULT CURRENT_TIMESTAMP,    
    FOREIGN KEY (id_partida) REFERENCES Partidas(id_partida),
    FOREIGN KEY (id_jugador) REFERENCES Jugadores(id_jugador)
);

CREATE TABLE IF NOT EXISTS Invitaciones (
    id_invitacion INT AUTO_INCREMENT PRIMARY KEY,
    id_invitador INT NOT NULL,
    id_invitado INT NOT NULL,
    estado ENUM('pendiente', 'aceptada', 'rechazada', 'expirada') DEFAULT 'pendiente',
    fecha_invitacion DATETIME DEFAULT CURRENT_TIMESTAMP,
    fecha_respuesta DATETIME,
    FOREIGN KEY (id_invitador) REFERENCES Jugadores(id_jugador),
    FOREIGN KEY (id_invitado) REFERENCES Jugadores(id_jugador),
    INDEX idx_estado (estado),
    INDEX idx_invitador (id_invitador),
    INDEX idx_invitado (id_invitado)
)