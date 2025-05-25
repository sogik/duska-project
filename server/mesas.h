#ifndef MESAS_H
#define MESAS_H

#include <pthread.h> // Añadido para pthread_mutex_t
#include "cartas.h"

#define MAX_MESAS 20
#define MAX_JUGADORES 4     // Ya existente según gamelogic.h
#define MAX_CARTAS_JUGADA 5 // Definición faltante

// Tipos de mesa para juegos de cartas
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
    Carta ultima_jugada[MAX_CARTAS_JUGADA]; // Array de cartas jugadas
    int num_cartas_jugadas;                 // Número de cartas en la jugada
    TipoCarta tipo_declarado;               // Tipo de carta declarado
    bool jugadores_vivos[MAX_JUGADORES];    // Estado de los jugadores
} Mesa;

extern Mesa mesas[MAX_MESAS];
extern int num_mesas;
extern pthread_mutex_t mutex_mesas;

// Prototipos de funciones
void crear_mesa_para_grupo(int grupo_id);
TipoMesa obtener_tipo_mesa(int grupo_id);
void notificar_mesa_a_grupo(int grupo_id);

#endif
