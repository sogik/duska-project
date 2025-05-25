#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <pthread.h>
#include <time.h>
#include "partidas.h"
#include "basedatos.h" // Si necesitas funciones de la base de datos
#include "server.h"    // Si necesitas funciones de autenticación

// Declaraciones externas para funciones del servidor principal
extern int obtener_grupo_id(const char *usuario);
extern void broadcast_to_group(int grupo_id, const char *message);

extern int es_lider_grupo(const char *usuario, int grupo_id);

// Variables globales
GameInfo *partidas_lista = NULL; // Inicializar la lista enlazada

// Corrección para obtener_partida_por_id
GameInfo *obtener_partida_por_id(int partida_id)
{
    GameInfo *partida = partidas_lista;

    while (partida != NULL)
    {
        if (partida->partida_id == partida_id)
        {
            return partida;
        }
        partida = partida->next; // Usar el campo next
    }

    return NULL;
}

// Corrección para iniciar_partida
int iniciar_partida(int partida_id)
{
    // Buscar la partida por ID
    GameInfo *partida = obtener_partida_por_id(partida_id);
    if (!partida)
        return -1;

    // Verificar que no esté ya iniciada
    if (partida->estado == ESTADO_ACTIVA)
        return -2;

    // Inicializar el estado de la partida
    partida->estado = ESTADO_ACTIVA;

    // Obtener lista de jugadores del grupo
    partida->num_jugadores = num_usuarios_grupo(partida->grupo_id); // Usar el campo num_jugadores
    if (partida->num_jugadores <= 0)
        return -3;

    // Asignar memoria para los nombres de jugadores
    partida->jugadores = (char **)malloc(partida->num_jugadores * sizeof(char *));
    if (!partida->jugadores)
        return -4;

    // Copiar los nombres de los jugadores
    for (int i = 0; i < partida->num_jugadores; i++)
    {
        char *nombre_jugador = obtener_usuario_grupo(partida->grupo_id, i);
        if (nombre_jugador)
        {
            partida->jugadores[i] = strdup(nombre_jugador); // Usar strdup para copiar el string
        }
        else
        {
            partida->jugadores[i] = NULL;
        }
    }

    // Asignar turno aleatorio inicial
    srand(time(NULL));
    partida->turno_actual = rand() % partida->num_jugadores;

    // Obtener el nombre del jugador con el turno actual
    char *jugador_actual = partida->jugadores[partida->turno_actual];
    if (!jugador_actual)
        return -5;

    // Notificar a todos los jugadores del inicio de partida
    char mensaje_inicio[100];
    snprintf(mensaje_inicio, sizeof(mensaje_inicio), "GAMESTART/OK");
    broadcast_to_group(partida->grupo_id, mensaje_inicio);

    // Notificar turno
    char mensaje_turno[100];
    snprintf(mensaje_turno, sizeof(mensaje_turno), "TURN/%s", jugador_actual);
    broadcast_to_group(partida->grupo_id, mensaje_turno);

    printf("Partida %d iniciada. Primer turno: %s\n", partida_id, jugador_actual);
    return 0;
}

// Corrección para avanzar_turno
int avanzar_turno(int partida_id)
{
    // Buscar la partida
    GameInfo *partida = obtener_partida_por_id(partida_id);
    if (!partida)
        return -1;

    // Verificar que la partida está activa
    if (partida->estado != ESTADO_ACTIVA)
        return -2;

    // Avanzar al siguiente turno
    partida->turno_actual = (partida->turno_actual + 1) % partida->num_jugadores;

    // Obtener el nombre del siguiente jugador
    char *siguiente_jugador = partida->jugadores[partida->turno_actual];
    if (!siguiente_jugador)
        return -3;

    // Notificar a todos los jugadores del nuevo turno
    char mensaje_turno[100];
    snprintf(mensaje_turno, sizeof(mensaje_turno), "TURN/%s", siguiente_jugador);
    broadcast_to_group(partida->grupo_id, mensaje_turno);

    printf("Partida %d: Turno avanzado a %s\n", partida_id, siguiente_jugador);
    return 0;
}

// Corrección de crear_partida
int crear_partida(int grupo_id)
{
    static int next_partida_id = 1;

    if (grupo_id <= 0)
        return -1;

    // Crear estructura de partida
    GameInfo *nueva_partida = (GameInfo *)malloc(sizeof(GameInfo));
    if (!nueva_partida)
        return -2;

    // Inicializar la partida
    nueva_partida->partida_id = next_partida_id++;
    nueva_partida->grupo_id = grupo_id;
    nueva_partida->estado = ESTADO_CREADA;
    nueva_partida->turno_actual = -1; // No hay turno asignado aún
    nueva_partida->num_jugadores = 0; // Se inicializará al iniciar la partida
    nueva_partida->jugadores = NULL;  // Se inicializará al iniciar la partida
    nueva_partida->next = NULL;

    // Añadir a la lista de partidas
    nueva_partida->next = partidas_lista;
    partidas_lista = nueva_partida;

    printf("Partida %d creada para grupo %d\n", nueva_partida->partida_id, grupo_id);
    return nueva_partida->partida_id;
}

// Añadir esta función para finalizar una partida
int finalizar_partida(int partida_id)
{
    // Buscar la partida en la lista
    GameInfo **pp = &partidas_lista;
    GameInfo *partida = *pp;

    while (partida != NULL)
    {
        if (partida->partida_id == partida_id)
        {
            // Liberar memoria de los nombres de jugadores
            if (partida->jugadores)
            {
                for (int i = 0; i < partida->num_jugadores; i++)
                {
                    free(partida->jugadores[i]);
                }
                free(partida->jugadores);
            }

            // Eliminar de la lista
            *pp = partida->next;
            free(partida);

            printf("Partida %d finalizada y eliminada\n", partida_id);
            return 0;
        }

        pp = &partida->next;
        partida = *pp;
    }

    printf("No se encontró la partida %d para finalizar\n", partida_id);
    return -1;
}