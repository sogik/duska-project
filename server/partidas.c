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

    partida->ronda_actual = 0;
    partida->total_rondas = 4;
    generar_carta_para_ronda_actual(partida);

    // Inicializar jugadores eliminados
    partida->num_jugadores_activos = partida->num_jugadores;
    for (int i = 0; i < 4; i++)
    {
        partida->jugadores_eliminados[i] = 0; // Todos activos al inicio
    }

    // Inicializar campos de eliminación pendiente
    partida->eliminacion_pendiente = 0;
    partida->jugador_pendiente_eliminacion[0] = '\0';

    // Notificar a todos los jugadores sobre el inicio de la partida
    char mensaje[100];
    snprintf(mensaje, sizeof(mensaje), "GAMESTART/OK\n"); // Añadir salto de línea
    broadcast_to_group(partida->grupo_id, mensaje);
    return 0;
}

void guardar_ultima_jugada(GameInfo *partida, const char *jugador, char cartas[][5], int num_cartas)
{
    if (!partida || !jugador || num_cartas <= 0)
        return;

    // Guardar información básica
    strncpy(partida->ultimo_jugador, jugador, sizeof(partida->ultimo_jugador) - 1);
    partida->num_cartas_ultima_jugada = num_cartas;

    // Copiar cada carta y su resultado de verificación
    for (int i = 0; i < num_cartas && i < 5; i++)
    {
        // Copiar la carta
        strncpy(partida->cartas_ultima_jugada[i], cartas[i], sizeof(partida->cartas_ultima_jugada[i]) - 1);
    }

    printf("[DESAFÍO] Guardada última jugada de %s: %d cartas\n",
           jugador, num_cartas);
}

int avanzar_ronda(int partida_id)
{
    GameInfo *partida = obtener_partida_por_id(partida_id);
    if (partida == NULL)
    {
        printf("[ERROR] No se encontró la partida %d\n", partida_id);
        return -1;
    }

    // Incrementar contador de ronda
    partida->ronda_actual++;

    // Verificar si se han completado todas las rondas
    if (partida->ronda_actual >= partida->total_rondas)
    {
        partida->estado = 2; // Finalizada
        printf("[PARTIDA] Partida %d finalizada, se completaron todas las rondas\n", partida_id);

        // Notificar fin de partida
        char mensaje[100];
        sprintf(mensaje, "FIN_PARTIDA/%d", partida_id);
        broadcast_to_group(partida->grupo_id, mensaje);

        return 2; // Código para indicar fin de partida
    }

    // Generar carta designada para esta nueva ronda
    generar_carta_para_ronda_actual(partida);

    printf("[RONDA] Partida %d avanzó a ronda %d, carta designada: %s\n",
           partida_id, partida->ronda_actual + 1, partida->cartas_ronda[partida->ronda_actual]);

    // Notificar a todos los jugadores sobre la nueva ronda
    char mensaje_ronda[100];
    sprintf(mensaje_ronda, "NUEVA_RONDA/%d/%s",
            partida->ronda_actual + 1, partida->cartas_ronda[partida->ronda_actual]);
    broadcast_to_group(partida->grupo_id, mensaje_ronda);

    return 0; // Éxito
}

// Eliminar un jugador de la partida y avanzar a la siguiente ronda
int eliminar_jugador_de_partida(GameInfo *partida, char *jugador)
{
    if (!partida || !jugador)
        return -1;

    // Buscar al jugador
    int indice_jugador = -1;
    for (int i = 0; i < partida->num_jugadores; i++)
    {
        if (strcmp(partida->jugadores[i], jugador) == 0)
        {
            indice_jugador = i;
            break;
        }
    }

    if (indice_jugador == -1)
    {
        printf("[ERROR] Jugador %s no encontrado en la partida %d\n",
               jugador, partida->partida_id);
        return -1;
    }

    // Marcar al jugador como eliminado
    partida->jugadores_eliminados[indice_jugador] = 1;
    partida->num_jugadores_activos--;

    printf("[PARTIDA] Jugador %s eliminado de la partida %d. Quedan %d jugadores activos.\n",
           jugador, partida->partida_id, partida->num_jugadores_activos);

    // Si quedan más de 1 jugador activo, avanzar a la siguiente ronda
    if (partida->num_jugadores_activos > 1)
    {
        // AVANZAR A LA SIGUIENTE RONDA
        partida->ronda_actual++;

        // Verificar si hemos excedido el número total de rondas
        if (partida->ronda_actual >= partida->total_rondas)
        {
            // Reiniciar al inicio si superamos el número de rondas
            partida->ronda_actual = 0;
        }

        // Generar nueva carta para la ronda
        generar_carta_para_ronda_actual(partida);

        printf("[RONDA] Avanzando a ronda %d, carta designada: %s\n",
               partida->ronda_actual + 1,
               partida->cartas_ronda[partida->ronda_actual]);

        // Notificar a todos sobre la nueva ronda
        // FORMATO MODIFICADO: Solo enviar el número de ronda, el cliente pedirá la carta después
        char mensaje_ronda[100];
        sprintf(mensaje_ronda, "NUEVA_RONDA/%d",
                partida->ronda_actual + 1);
        broadcast_to_group(partida->grupo_id, mensaje_ronda);
    }

    // Si el jugador eliminado tenía el turno, avanzar al siguiente jugador activo
    if (partida->turno_actual == indice_jugador)
    {
        // Buscar el siguiente jugador activo
        int nuevo_turno = (indice_jugador + 1) % partida->num_jugadores;
        while (partida->jugadores_eliminados[nuevo_turno] && nuevo_turno != indice_jugador)
        {
            nuevo_turno = (nuevo_turno + 1) % partida->num_jugadores;
        }

        partida->turno_actual = nuevo_turno;

        // Notificar el nuevo turno
        char mensaje_turno[100];
        sprintf(mensaje_turno, "TURN/%s", partida->jugadores[partida->turno_actual]);
        broadcast_to_group(partida->grupo_id, mensaje_turno);
    }

    return 0;
}

// Generar las cartas designadas para cada ronda
void generar_carta_para_ronda_actual(GameInfo *partida)
{
    // Definir los 4 tipos de cartas disponibles
    const char *tipos_cartas[] = {"ACES", "REYES", "REINAS", "JOKERS"};
    int num_tipos = 4;

    // Asignar un tipo de carta aleatoriamente
    int indice_tipo = rand() % num_tipos;

    // Guardar el tipo de carta para esta ronda
    strncpy(partida->cartas_ronda[partida->ronda_actual], tipos_cartas[indice_tipo],
            sizeof(partida->cartas_ronda[partida->ronda_actual]) - 1);

    printf("[RONDA] Ronda %d: Carta designada '%s'\n",
           partida->ronda_actual + 1, partida->cartas_ronda[partida->ronda_actual]);
}

// Obtener la carta designada para la ronda actual
void obtener_carta_ronda_actual(GameInfo *partida, char *carta_ronda)
{
    if (partida != NULL && partida->ronda_actual < partida->total_rondas)
    {
        strcpy(carta_ronda, partida->cartas_ronda[partida->ronda_actual]);
    }
    else
    {
        strcpy(carta_ronda, "?");
    }
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
    /*char chat_mensaje[200];
    snprintf(chat_mensaje, sizeof(chat_mensaje), "CHAT/SISTEMA/*** TURNO DE %s ***\n",
             siguiente_jugador);
    broadcast_to_group(partida->grupo_id, chat_mensaje);*/

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