#ifndef GAMELOGIC_H
#define GAMELOGIC_H

#include "mesas.h"
#include <stdbool.h>
#include <pthread.h>

#define MAX_JUGADORES 4
#define CARTAS_POR_JUGADOR 5

// Tipos de carta (distintos del TipoMesa)
typedef enum
{
    CARD_AS,
    CARD_REINA,
    CARD_REY,
    CARD_JOKER
} TipoCarta;

typedef struct
{
    TipoCarta tipo;
    int valor;
} Carta;

// Funciones de gamelogic
void manejar_jugada(int grupo_id, int jugador_id, Carta carta_jugada);
int obtener_ganador(int grupo_id);
int comprobar_verdad(int grupo_id, int jugador_reta, int jugador_retado);

#endif
