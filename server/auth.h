#ifndef AUTH_H
#define AUTH_H

int registrarUsuario(MYSQL *conn, const char* nombre_usuario, const char* contrasena);
int iniciarSesion(MYSQL *conn, const char* nombre_usuario, const char* contrasena);

#endif