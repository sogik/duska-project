#ifndef MESAS_H
#define MESAS_H

#include "cartas.h"
#include <pthread.h>

#define MAX_JUGADORES_MESA 4

typedef enum
{
    MESA_ASES,
    MESA_REINAS,
    MESA_REYES
} TipoMesa;

typedef struct
{
    int grupo_id;
    int jugador_actual;                       // Índice del jugador que tiene el turno
    int jugador_ultimo;                       // Índice del último jugador que jugó cartas
    bool jugadores_vivos[MAX_JUGADORES_MESA]; // Estado de cada jugador
    TipoMesa tipo;                            // Tipo actual de la mesa (ases, reinas, reyes)
    Carta ultima_jugada;                      // Última carta jugada
} Mesa;

#define MAX_MESAS 50
extern Mesa mesas[MAX_MESAS];
extern int num_mesas;
extern pthread_mutex_t mutex_mesas;

void crear_mesa_para_grupo(int grupo_id);
TipoMesa obtener_tipo_mesa(int grupo_id);
void notificar_mesa_a_grupo(int grupo_id);

#endif
