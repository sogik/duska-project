#include "chat.h"
#include <string.h>
#include <stdio.h>

pthread_mutex_t mutex_chat = PTHREAD_MUTEX_INITIALIZER;

void iniciar_chat() {
    // Inicialización del subsistema de chat (si es necesaria)
}

void manejar_mensaje_chat(const char* mensaje, int remitente_id) {
    char mensaje_formateado[1024];
    char nombre_remitente[50] = "Desconocido";
    
    // Bloquear acceso a la lista de jugadores
    pthread_mutex_lock(&mutex_jugadores);
    for(int i = 0; i < num_jugadores; i++) {
        if(jugadores[i].id_jugador == remitente_id) {
            strncpy(nombre_remitente, jugadores[i].nombre, 49);
            break;
        }
    }
    pthread_mutex_unlock(&mutex_jugadores);
    
    // Formatear mensaje
    snprintf(mensaje_formateado, sizeof(mensaje_formateado), 
            "[CHAT][%s]: %s", 
            nombre_remitente, 
            mensaje);
    
    // Enviar a todos los jugadores en partida
    broadcast_chat(mensaje_formateado, true);
}

void broadcast_chat(const char* mensaje, bool solo_partida) {
    pthread_mutex_lock(&mutex_jugadores);
    
    for(int i = 0; i < num_jugadores; i++) {
        if(!solo_partida || jugadores[i].en_partida) {
            char protocolo[1024];
            snprintf(protocolo, sizeof(protocolo), "CHAT:%s", mensaje);
            write(jugadores[i].socket, protocolo, strlen(protocolo));
        }
    }
    
    pthread_mutex_unlock(&mutex_jugadores);
}
