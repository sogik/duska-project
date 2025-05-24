#ifndef GAMELOGIC_H
#define GAMELOGIC_H

#include "cartas.h"
#include "mesas.h"
#include <stdbool.h>
#include <pthread.h>

#define MAX_JUGADORES 4
#define CARTAS_POR_JUGADOR 5

// Ya no necesitamos definir estos tipos aquí, vienen de common_types.h
// Funciones de gamelogic
void manejar_jugada(int grupo_id, int jugador_id, Carta carta_jugada);
int obtener_ganador(int grupo_id);
int comprobar_verdad(int grupo_id, int jugador_reta, int jugador_retado);
void notificar_estado_grupo(int grupo_id);
void expulsar_jugador(int grupo_id, int jugador_id);
int registrar_jugada_multiple(int grupo_id, int jugador_id, int tipos_cartas[], int cantidad);
int comprobar_mentira_ultima_jugada(int grupo_id, int jugador_acusador);
int existe_mesa_para_grupo(int grupo_id);
int obtener_id_jugador_en_grupo(const char *usuario, int grupo_id);

#endif
