#include "conexiones.h"
#include <string.h>
#include <stdio.h>

Conexion conexiones_activas[MAX_CONEXIONES];
int num_conexiones_activas = 0;
pthread_mutex_t mutex_conexiones = PTHREAD_MUTEX_INITIALIZER;

void agregar_conexion(int id_jugador, int socket, const char* usuario) {
    pthread_mutex_lock(&mutex_conexiones);
    
    if(num_conexiones_activas < MAX_CONEXIONES) {
        conexiones_activas[num_conexiones_activas].id_jugador = id_jugador;
        conexiones_activas[num_conexiones_activas].socket = socket;
        strncpy(conexiones_activas[num_conexiones_activas].nombre_usuario, usuario, 50);
        num_conexiones_activas++;
    }
    
    pthread_mutex_unlock(&mutex_conexiones);
    broadcast_usuarios_conectados(); // Notificar a todos
}

void eliminar_conexion(int socket) {
    pthread_mutex_lock(&mutex_conexiones);
    
    for(int i = 0; i < num_conexiones_activas; i++) {
        if(conexiones_activas[i].socket == socket) {
            // Eliminar moviendo el último elemento
            conexiones_activas[i] = conexiones_activas[num_conexiones_activas - 1];
            num_conexiones_activas--;
            break;
        }
    }
    
    pthread_mutex_unlock(&mutex_conexiones);
    broadcast_usuarios_conectados(); // Notificar a todos
}

char* obtener_lista_usuarios() {
    static char lista[1024];
    lista[0] = '\0';
    
    pthread_mutex_lock(&mutex_conexiones);
    for(int i = 0; i < num_conexiones_activas; i++) {
        strcat(lista, conexiones_activas[i].nombre_usuario);
        if(i != num_conexiones_activas - 1) strcat(lista, ",");
    }
    pthread_mutex_unlock(&mutex_conexiones);
    
    return lista;
}

void broadcast_usuarios_conectados() {
    char mensaje[1024];
    snprintf(mensaje, sizeof(mensaje), "USUARIOS_CONECTADOS:%s", obtener_lista_usuarios());
    
    pthread_mutex_lock(&mutex_conexiones);
    for(int i = 0; i < num_conexiones_activas; i++) {
        send(conexiones_activas[i].socket, mensaje, strlen(mensaje), 0);
    }
    pthread_mutex_unlock(&mutex_conexiones);
}
