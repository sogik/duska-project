#ifndef MESAS_H
#define MESAS_H

#include "cartas.h"

// Definir constantes
#define MAX_JUGADORES 4
#define MAX_CARTAS_JUGADA 5 // Número máximo de cartas que se pueden jugar de una vez

typedef enum
{
    MESA_ASES,
    MESA_REINAS,
    MESA_REYES
} TipoMesa;

typedef struct
{
    int grupo_id;
    TipoMesa tipo;
    int jugador_ultimo;
    bool jugadores_vivos[MAX_JUGADORES];

    // Modificar para soportar múltiples cartas
    Carta ultima_jugada[MAX_CARTAS_JUGADA]; // Array de cartas jugadas
    int num_cartas_jugadas;                 // Número de cartas en la última jugada
    TipoCarta tipo_declarado;               // Tipo de carta que el jugador dice estar jugando
} Mesa;

#define MAX_MESAS 50
extern Mesa mesas[MAX_MESAS];
extern int num_mesas;
extern pthread_mutex_t mutex_mesas;

void crear_mesa_para_grupo(int grupo_id);
TipoMesa obtener_tipo_mesa(int grupo_id);
void notificar_mesa_a_grupo(int grupo_id);

#endif
