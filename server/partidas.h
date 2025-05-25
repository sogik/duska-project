#ifndef PARTIDAS_H
#define PARTIDAS_H

// Definiciones para el estado de la partida
#define ESTADO_CREADA 0
#define ESTADO_ACTIVA 1
#define ESTADO_FINALIZADA 2

// Estructura para almacenar información de la partida
typedef struct GameInfo
{
    int partida_id;
    int grupo_id;
    int estado;
    int turno_actual;
    int num_jugadores;     // Agregar este campo
    char **jugadores;      // Agregar este campo (array de nombres de jugadores)
    struct GameInfo *next; // Agregar este campo (para lista enlazada)
} GameInfo;

// Variables globales (declaradas como extern)
extern GameInfo *partidas_lista; // Lista enlazada de partidas

// Prototipos de funciones
int crear_partida(int grupo_id);
int iniciar_partida(int partida_id);
int es_turno_de_jugador(const char *usuario);
GameInfo *obtener_partida_por_jugador(const char *usuario);
GameInfo *obtener_partida_por_id(int partida_id);
int avanzar_turno(int partida_id);
int finalizar_partida(int partida_id);

#endif