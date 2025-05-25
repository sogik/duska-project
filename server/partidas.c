#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <time.h>
#include "partidas.h"

// Implementación para la lista global de partidas
GameInfo *partidas_lista = NULL;

// Función para crear una partida
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
    nueva_partida->estado = 0; // ESTADO_CREADA
    nueva_partida->turno_actual = -1;
    nueva_partida->num_jugadores = 0;
    nueva_partida->jugadores = NULL;
    nueva_partida->next = partidas_lista;
    partidas_lista = nueva_partida;

    printf("Partida %d creada para grupo %d\n", nueva_partida->partida_id, grupo_id);
    return nueva_partida->partida_id;
}

// Obtener partida por ID
GameInfo *obtener_partida_por_id(int partida_id)
{
    GameInfo *partida = partidas_lista;

    while (partida != NULL)
    {
        if (partida->partida_id == partida_id)
            return partida;

        partida = partida->next;
    }

    return NULL;
}

// Función para iniciar una partida
int iniciar_partida(int partida_id)
{
    // Buscar la partida
    GameInfo *partida = obtener_partida_por_id(partida_id);
    if (!partida)
        return -1;

    // Marcar como activa
    partida->estado = 1; // ESTADO_ACTIVA

    // Obtener jugadores
    int num_jug = num_usuarios_grupo(partida->grupo_id);
    if (num_jug <= 0)
        return -2;

    partida->num_jugadores = num_jug;

    // Reservar memoria para los nombres de los jugadores
    partida->jugadores = (char **)malloc(num_jug * sizeof(char *));
    if (!partida->jugadores)
        return -3;

    // Copiar nombres de jugadores
    for (int i = 0; i < num_jug; i++)
    {
        char *nombre = obtener_usuario_grupo(partida->grupo_id, i);
        if (nombre)
            partida->jugadores[i] = strdup(nombre);
        else
            partida->jugadores[i] = NULL;
    }

    // Asignar turno inicial aleatorio
    srand(time(NULL));
    partida->turno_actual = rand() % num_jug;

    // Notificar a todos los jugadores sobre el inicio de la partida
    char mensaje[100];
    snprintf(mensaje, sizeof(mensaje), "GAMESTART/OK\n"); // Añadir salto de línea
    broadcast_to_group(partida->grupo_id, mensaje);

    // Esperar un momento para asegurar que el mensaje llegue por separado
    usleep(200000); // 200ms de pausa

    // Notificar el primer turno como un mensaje separado
    char mensaje_turno[100];
    snprintf(mensaje_turno, sizeof(mensaje_turno), "TURN/%s\n",
             partida->jugadores[partida->turno_actual]);
    printf("[PARTIDA] Enviando primer turno: '%s'\n", mensaje_turno);
    broadcast_to_group(partida->grupo_id, mensaje_turno);

    return 0;
}

// Función para verificar si es el turno de un jugador
int es_turno_de_jugador(const char *usuario)
{
    if (!usuario)
        return 0;

    // Buscar la partida del jugador
    GameInfo *partida = obtener_partida_por_jugador(usuario);
    if (!partida || partida->estado != 1) // ESTADO_ACTIVA
        return 0;

    if (!partida->jugadores || partida->turno_actual < 0 ||
        partida->turno_actual >= partida->num_jugadores)
        return 0;

    // IMPORTANTE: Comparación insensible a mayúsculas/minúsculas
    char *jugador_turno = partida->jugadores[partida->turno_actual];
    if (!jugador_turno)
        return 0;

    // Usar strcasecmp para comparación insensible a mayúsculas
    int es_turno = (strcasecmp(jugador_turno, usuario) == 0);

    printf("[TURNO] Verificando turno - Turno actual: '%s', Usuario: '%s', Resultado: %s\n",
           jugador_turno, usuario, es_turno ? "SI" : "NO");

    return es_turno;
}

// Función para obtener la partida de un jugador
GameInfo *obtener_partida_por_jugador(const char *usuario)
{
    if (!usuario)
        return NULL;

    // Obtener el grupo del jugador
    int grupo_id = obtener_grupo_id(usuario);
    if (grupo_id <= 0)
        return NULL;

    // Buscar una partida activa para este grupo
    GameInfo *partida = partidas_lista;
    while (partida != NULL)
    {
        if (partida->grupo_id == grupo_id && partida->estado == 1) // ESTADO_ACTIVA
            return partida;

        partida = partida->next;
    }

    return NULL;
}

// Función para avanzar el turno
int avanzar_turno(int partida_id)
{
    // Buscar partida
    GameInfo *partida = obtener_partida_por_id(partida_id);
    if (!partida || partida->estado != 1)
        return -1;

    // Avanzar turno
    partida->turno_actual = (partida->turno_actual + 1) % partida->num_jugadores;

    // Obtener nombre del siguiente jugador
    char *siguiente_jugador = partida->jugadores[partida->turno_actual];
    if (!siguiente_jugador)
        return -2;

    // Enviar mensaje de turno SEPARADO con salto de línea
    char mensaje_turno[100];
    snprintf(mensaje_turno, sizeof(mensaje_turno), "TURN/%s\n", siguiente_jugador);
    printf("[TURNO] Enviando mensaje de turno: '%s'\n", mensaje_turno);

    // Enviar mensaje de turno
    broadcast_to_group(partida->grupo_id, mensaje_turno);

    // También enviar mensaje de turno como un mensaje de chat
    char chat_mensaje[200];
    snprintf(chat_mensaje, sizeof(chat_mensaje), "CHAT/SISTEMA/*** TURNO DE %s ***\n",
             siguiente_jugador);
    broadcast_to_group(partida->grupo_id, chat_mensaje);

    return 0;
}

// Función para finalizar una partida
int finalizar_partida(int partida_id)
{
    GameInfo **pp = &partidas_lista;

    while (*pp != NULL)
    {
        GameInfo *partida = *pp;

        if (partida->partida_id == partida_id)
        {
            // Liberar memoria de jugadores
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
            return 0;
        }

        pp = &(*pp)->next;
    }

    return -1;
}