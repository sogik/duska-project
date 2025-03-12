#ifndef DATABASE_H
#define DATABASE_H

#include <mysql.h>

int usuarioExiste(MYSQL *conn, const char* nombre_usuario);
int insertarUsuario(MYSQL *conn, const char* nombre_usuario, const char* contrasena);
int verificarCredenciales(MYSQL *conn, const char* nombre_usuario, const char* contrasena);
int desconectar_base_datos(MYSQL *conn);
void listarJugadores(MYSQL *conn, char *lista, int tamano_lista);
char* listarPartidas(MYSQL *conn);
char* listarPartidasGanadas(MYSQL *conn, const char* nombre_usuario);

#endif