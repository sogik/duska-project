#ifndef SERVER_H
#define SERVER_H

// Funciones de comunicación implementadas en server.c
int broadcast_to_group(int grupo_id, const char *mensaje);
int send_to_user(const char *destinatario, const char *mensaje);
int es_lider_grupo(const char *usuario, int grupo_id);
void disolver_grupo(int grupo_id);
int broadcast_to_group_except(int grupo_id, const char *mensaje, int socket_excluido);
int obtener_grupo_id(const char *usuario);
int es_lider_grupo(const char *usuario, int grupo_id);
int num_usuarios_grupo(int grupo_id);
char *obtener_usuario_grupo(int grupo_id, int indice);

#endif