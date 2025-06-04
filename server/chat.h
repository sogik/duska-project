#ifndef CHAT_H
#define CHAT_H

#include "conexiones.h"
#include <pthread.h>

// Prototipos de funciones
void iniciar_chat();
void manejar_mensaje_chat(const char* mensaje, int remitente_id);
void broadcast_chat(const char* mensaje, bool solo_partida);

#endif
