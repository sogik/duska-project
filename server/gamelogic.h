#ifndef GAMELOGIC_H
#define GAMELOGIC_H

#include "mesas.h"
#include <stdbool.h>
#include <pthread.h>

#define MAX_JUGADORES 4
#define CARTAS_POR_JUGADOR 4

typedef enum { 
    CARD_AS, 
    CARD_REINA, 
    CARD_REY,
    CARD_JOKER
} TipoCarta;

typedef struct {
    TipoCarta tipo;
    int valor;
} Carta;

typedef struct {
    int id;
    int socket;
    Carta mano[CARTAS_POR_JUGADOR];
    bool en_partida;
} Jugador;

typedef enum {
    MESA_ASES,
    MESA_REINAS,
    MESA_REYES
} TipoMesa;

typedef struct {
    int grupo_id;
    TipoMesa tipo;
    Carta ultima_jugada;
    int jugador_ultimo; 
    bool jugadores_vivos[MAX_JUGADORES];
} Mesa;

extern Mesa mesas[MAX_MESAS];
extern int num_mesas;
extern pthread_mutex_t mutex_mesas;

// Lógica de juego
void iniciar_partida(Jugador jugadores[MAX_JUGADORES]);
void manejar_jugada(int grupo_id, int jugador_id, Carta carta_jugada);
int comprobar_verdad(int grupo_id, int jugador_que_reta, int jugador_retentado);
void notificar_estado_grupo(int grupo_id);
int obtener_ganador(int grupo_id);
void limpiar_mesa(int grupo_id);
void expulsar_jugador(int grupo_id, int jugador_id);


#endif
