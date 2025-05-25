#ifndef CONEXIONES_H
#define CONEXIONES_H

#include <mysql.h>
#include <pthread.h>
#include "server.h"

#define MAX_CONEXIONES 100

typedef struct
{
    int id_jugador;
    int socket;
    char nombre_usuario[50];
} Conexion;

// Variables globales con bloqueo mutex
extern Conexion conexiones_activas[MAX_CONEXIONES];
extern int num_conexiones_activas;
extern pthread_mutex_t mutex_conexiones;

// Funciones
void agregar_conexion(int id_jugador, int socket, const char *usuario);
void eliminar_conexion(int socket);
void broadcast_usuarios_conectados();
char *obtener_lista_usuarios();

// Funciones de comunicación
void broadcast_to_group(int grupo_id, const char *message);
void broadcast_to_all(const char *message);
int send_to_user(const char *destinatario, const char *mensaje);
int enviar_mensaje_a_usuario(const char *destinatario, const char *mensaje);

#endif