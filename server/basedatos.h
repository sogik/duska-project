#ifndef DATABASE_H
#define DATABASE_H

int usuarioExiste(const char* nombre_usuario);
int insertarUsuario(const char* nombre_usuario, const char* contrasena);
int verificarCredenciales(const char* nombre_usuario, const char* contrasena);
int conexion_base_datos();
int desconectar_base_datos();
void listarJugadores();
void listarPartidas();
void listarPartidasGanadas(const char* nombre_usuario);

#endif