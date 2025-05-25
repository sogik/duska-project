#ifndef GAMELOGIC_H
#define GAMELOGIC_H

#include "cartas.h"
#include "mesas.h"
#include <stdbool.h>
#include <pthread.h>

#define MAX_JUGADORES 4
#define CARTAS_POR_JUGADOR 5

// Estructuras para el juego del mentiroso
typedef struct
{
    TipoCarta tipo;
    int cantidad;
    char jugador[50];
} JugadaMentiroso;

// Funciones existentes para la lógica del juego
void manejar_jugada(int grupo_id, int jugador_id, Carta carta_jugada);
int obtener_ganador(int grupo_id);
int comprobar_verdad(int grupo_id, int jugador_reta, int jugador_retado);
void notificar_estado_grupo(int grupo_id);
void expulsar_jugador(int grupo_id, int jugador_id);
void limpiar_mesa(int grupo_id);

// Nuevas funciones para el juego del mentiroso
void procesar_jugada_mentiroso(int grupo_id, const char *jugador, const char *cartas_str, const char *tipo_declarado);
int procesar_acusacion(int grupo_id, const char *acusador, const char *acusado);
void avanzar_turno(int grupo_id);
void notificar_turno_actual(int grupo_id);
void cambiar_tipo_mesa(int grupo_id);
int obtener_indice_jugador(int grupo_id, const char *nombre_usuario);
void obtener_nombre_usuario(int grupo_id, int indice, char *nombre);
void inicializar_juego_mentiroso(int grupo_id);
void repartir_cartas_iniciales(int grupo_id);

#endif
