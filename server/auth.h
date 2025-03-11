#ifndef AUTH_H
#define AUTH_H

int registrarUsuario(const char* nombre_usuario, const char* contrasena);
int iniciarSesion(const char* nombre_usuario, const char* contrasena);

#endif