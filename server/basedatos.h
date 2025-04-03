#ifndef BASEDATOS_H
#define BASEDATOS_H

#include <mysql.h>

typedef struct {
    int id_jugador;
    int socket;
} ConexionJugador;

// Gestión de conexiones
extern ConexionJugador conexiones[50];
extern int num_conexiones;
int buscar_socket_por_id(int id_jugador);
void registrar_conexion(int id_jugador, int socket);

// Funciones de base de datos
int desconectar_base_datos(MYSQL *conn);
int usuarioExiste(MYSQL *conn, const char* nombre_usuario);
int insertarUsuario(MYSQL *conn, const char* nombre_usuario, const char* contrasena);
int verificarCredenciales(MYSQL *conn, const char* nombre_usuario, const char* contrasena);
int actualizarEstado(MYSQL *conn, const char* nombre_usuario, int estado);
void listarJugadores(MYSQL *conn, char *lista, int tamano_lista);
void listarPartidas(MYSQL *conn, char *lista, int tamano_lista);
void listarPartidasGanadas(MYSQL *conn, const char* nombre_usuario, char *lista, int tamano_lista);
void listarConectados(MYSQL *conn, char *lista, int tamano_lista);

#endif
