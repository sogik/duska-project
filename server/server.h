#ifndef SERVER_H
#define SERVER_H

// Funciones de comunicación implementadas en server.c
void broadcast_to_group(int grupo_id, const char *message);
int send_to_user(const char *destinatario, const char *mensaje);
int es_lider_grupo(const char *usuario, int grupo_id);
void disolver_grupo(int grupo_id);

#endif