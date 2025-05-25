#ifndef PARTIDAS_H
#define PARTIDAS_H

// Definiciones para el estado de la partida
#define ESTADO_CREADA 0
#define ESTADO_ACTIVA 1
#define ESTADO_FINALIZADA 2

// Estructura para almacenar información de la partida
typedef struct
{
    int partida_id;
    int grupo_id;
    int estado;
    int turno_actual;
    // Otros datos de partida según necesidad
} GameInfo;

// Variables globales (declaradas como extern)
extern GameInfo *partidas;
extern int num_partidas;

// Prototipos de funciones
int crear_partida(int grupo_id);
int iniciar_partida(int partida_id);
int es_turno_de_jugador(const char *usuario);
GameInfo *obtener_partida_por_jugador(const char *usuario);
GameInfo *obtener_partida_por_id(int partida_id);
int avanzar_turno(int partida_id);

#endif