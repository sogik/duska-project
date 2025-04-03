DROP DATABASE IF EXISTS duska_project;

CREATE DATABASE duska_project;

USE duska_project;

CREATE TABLE Jugadores (
    id_jugador INT AUTO_INCREMENT PRIMARY KEY,
    nombre_usuario VARCHAR(50) UNIQUE NOT NULL,
    contrasena VARCHAR(255) NOT NULL,
    fecha_registro DATETIME DEFAULT CURRENT_TIMESTAMP,
    ultima_conexion DATETIME DEFAULT CURRENT_TIMESTAMP,
    estado INT DEFAULT 0
);

CREATE TABLE Partidas (
    id_partida INT AUTO_INCREMENT PRIMARY KEY,
    fecha_inicio DATETIME DEFAULT CURRENT_TIMESTAMP,
    duracion INT NOT NULL,
    ganador_id INT NOT NULL,
    FOREIGN KEY (ganador_id) REFERENCES Jugadores(id_jugador)
);

CREATE TABLE Participantes (
    id_partida INT NOT NULL,
    id_jugador INT NOT NULL,
    fecha_participacion DATETIME DEFAULT CURRENT_TIMESTAMP,    
    FOREIGN KEY (id_partida) REFERENCES Partidas(id_partida),
    FOREIGN KEY (id_jugador) REFERENCES Jugadores(id_jugador)
);

CREATE TABLE Invitaciones (
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