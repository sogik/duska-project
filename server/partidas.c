#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <pthread.h>
#include <time.h>
#include "partidas.h"
#include "basedatos.h" // Si necesitas funciones de la base de datos

// Declaraciones externas para funciones del servidor principal
extern int obtener_grupo_id(const char *usuario);
extern void broadcast_to_group(int grupo_id, const char *message);

// Lista de partidas activas
static GameInfo *partidas_activas = NULL;
static pthread_mutex_t partidas_mutex = PTHREAD_MUTEX_INITIALIZER;

// Contador para IDs de partidas
static int next_partida_id = 1;
static pthread_mutex_t partida_id_mutex = PTHREAD_MUTEX_INITIALIZER;

// Función para obtener un nuevo ID de partida
int get_new_partida_id()
{
    pthread_mutex_lock(&partida_id_mutex);
    int id = next_partida_id++;
    pthread_mutex_unlock(&partida_id_mutex);
    return id;
}

// Función para crear una nueva partida para un grupo
int crear_partida(int grupo_id)
{
    pthread_mutex_lock(&partidas_mutex);

    // Verificar si ya existe una partida para este grupo
    GameInfo *actual = partidas_activas;
    while (actual != NULL)
    {
        if (actual->grupo_id == grupo_id && actual->partida_iniciada)
        {
            pthread_mutex_unlock(&partidas_mutex);
            return -1; // Ya existe una partida activa para este grupo
        }
        actual = actual->next;
    }

    // Crear nueva partida
    GameInfo *nueva_partida = (GameInfo *)malloc(sizeof(GameInfo));
    if (!nueva_partida)
    {
        pthread_mutex_unlock(&partidas_mutex);
        return -2; // Error de memoria
    }

    // Inicializar la partida
    nueva_partida->partida_id = get_new_partida_id();
    nueva_partida->grupo_id = grupo_id;
    nueva_partida->num_jugadores = 0;
    nueva_partida->turno_actual = -1;
    nueva_partida->partida_iniciada = 0;
    nueva_partida->next = partidas_activas;

    // Obtener jugadores del grupo (necesitarás implementar esto)
    // Esta función dependerá de cómo gestionas los grupos en tu servidor
    char jugadores[10][50];
    int num_jugadores = obtener_jugadores_grupo(grupo_id, jugadores);

    if (num_jugadores < 2)
    {
        free(nueva_partida);
        pthread_mutex_unlock(&partidas_mutex);
        return -3; // No hay suficientes jugadores
    }

    // Copiar jugadores a la estructura
    nueva_partida->num_jugadores = num_jugadores;
    for (int i = 0; i < num_jugadores; i++)
    {
        strcpy(nueva_partida->jugadores[i], jugadores[i]);
    }

    // Añadir la partida a la lista
    partidas_activas = nueva_partida;
    int partida_id = nueva_partida->partida_id;

    pthread_mutex_unlock(&partidas_mutex);
    return partida_id;
}

// Función auxiliar para obtener jugadores de un grupo
int obtener_jugadores_grupo(int grupo_id, char jugadores[10][50])
{
    // Esta función debe ser implementada para obtener todos los jugadores
    // de un grupo específico desde tu estructura de datos de grupos

    // Por ahora, usa una función externa declarada en server.c
    extern int listar_jugadores_grupo(int grupo_id, char jugadores[10][50]);
    return listar_jugadores_grupo(grupo_id, jugadores);
}

// Función para iniciar una partida y asignar el primer turno aleatoriamente
int iniciar_partida(int partida_id)
{
    pthread_mutex_lock(&partidas_mutex);

    GameInfo *partida = partidas_activas;
    while (partida != NULL)
    {
        if (partida->partida_id == partida_id)
        {
            if (partida->partida_iniciada)
            {
                pthread_mutex_unlock(&partidas_mutex);
                return -1; // La partida ya está iniciada
            }

            // Barajar el array de jugadores para asignar orden aleatorio
            // (Algoritmo Fisher-Yates shuffle)
            for (int i = partida->num_jugadores - 1; i > 0; i--)
            {
                int j = rand() % (i + 1);
                // Intercambiar jugadores[i] y jugadores[j]
                char temp[50];
                strcpy(temp, partida->jugadores[i]);
                strcpy(partida->jugadores[i], partida->jugadores[j]);
                strcpy(partida->jugadores[j], temp);
            }

            // Establecer el turno inicial al primer jugador
            partida->turno_actual = 0;
            partida->partida_iniciada = 1;

            pthread_mutex_unlock(&partidas_mutex);

            // Preparar y enviar notificación a todos los jugadores sobre el inicio
            char mensaje_inicio[1024];
            snprintf(mensaje_inicio, sizeof(mensaje_inicio), "GAMESTART/%d", partida_id);

            // Añadir la lista de jugadores en el orden determinado
            for (int i = 0; i < partida->num_jugadores; i++)
            {
                char jugador_info[60];
                snprintf(jugador_info, sizeof(jugador_info), "/%s", partida->jugadores[i]);
                strcat(mensaje_inicio, jugador_info);
            }

            // Notificar a todos los miembros del grupo
            broadcast_to_group(partida->grupo_id, mensaje_inicio);

            // Enviar notificación del primer turno
            char mensaje_turno[256];
            snprintf(mensaje_turno, sizeof(mensaje_turno), "TURN/%s", partida->jugadores[0]);
            broadcast_to_group(partida->grupo_id, mensaje_turno);

            return 0; // Éxito
        }
        partida = partida->next;
    }

    pthread_mutex_unlock(&partidas_mutex);
    return -2; // Partida no encontrada
}

// Función para obtener información de una partida por ID
GameInfo *obtener_partida_por_id(int partida_id)
{
    pthread_mutex_lock(&partidas_mutex);

    GameInfo *partida = partidas_activas;
    while (partida != NULL)
    {
        if (partida->partida_id == partida_id)
        {
            pthread_mutex_unlock(&partidas_mutex);
            return partida;
        }
        partida = partida->next;
    }

    pthread_mutex_unlock(&partidas_mutex);
    return NULL;
}

// Función para obtener la partida en la que participa un jugador
GameInfo *obtener_partida_por_jugador(const char *usuario)
{
    pthread_mutex_lock(&partidas_mutex);

    int grupo_id = obtener_grupo_id(usuario);
    if (grupo_id <= 0)
    {
        pthread_mutex_unlock(&partidas_mutex);
        return NULL;
    }

    GameInfo *partida = partidas_activas;
    while (partida != NULL)
    {
        if (partida->grupo_id == grupo_id && partida->partida_iniciada)
        {
            pthread_mutex_unlock(&partidas_mutex);
            return partida;
        }
        partida = partida->next;
    }

    pthread_mutex_unlock(&partidas_mutex);
    return NULL;
}

// Función para verificar si es el turno de un jugador
int es_turno_de_jugador(const char *usuario)
{
    pthread_mutex_lock(&partidas_mutex);

    GameInfo *partida = partidas_activas;
    while (partida != NULL)
    {
        if (partida->partida_iniciada)
        {
            // Verificar si el jugador está en esta partida y es su turno
            if (partida->turno_actual >= 0 &&
                partida->turno_actual < partida->num_jugadores &&
                strcmp(partida->jugadores[partida->turno_actual], usuario) == 0)
            {
                pthread_mutex_unlock(&partidas_mutex);
                return 1; // Es su turno
            }
        }
        partida = partida->next;
    }

    pthread_mutex_unlock(&partidas_mutex);
    return 0; // No es su turno
}

// Función para avanzar al siguiente turno
int avanzar_turno(int partida_id)
{
    pthread_mutex_lock(&partidas_mutex);

    GameInfo *partida = partidas_activas;
    while (partida != NULL)
    {
        if (partida->partida_id == partida_id)
        {
            if (!partida->partida_iniciada)
            {
                pthread_mutex_unlock(&partidas_mutex);
                return -1; // Partida no iniciada
            }

            // Avanzar al siguiente jugador
            partida->turno_actual = (partida->turno_actual + 1) % partida->num_jugadores;

            // Obtener el nuevo jugador con turno
            char *siguiente_jugador = partida->jugadores[partida->turno_actual];

            pthread_mutex_unlock(&partidas_mutex);

            // Notificar a todos los jugadores
            char mensaje_turno[256];
            snprintf(mensaje_turno, sizeof(mensaje_turno), "TURN/%s", siguiente_jugador);
            broadcast_to_group(partida->grupo_id, mensaje_turno);

            return 0; // Éxito
        }
        partida = partida->next;
    }

    pthread_mutex_unlock(&partidas_mutex);
    return -2; // Partida no encontrada
}

// Función para finalizar una partida
int finalizar_partida(int partida_id, const char *resultado)
{
    pthread_mutex_lock(&partidas_mutex);

    GameInfo **pp = &partidas_activas;
    while (*pp != NULL)
    {
        if ((*pp)->partida_id == partida_id)
        {
            GameInfo *partida = *pp;
            int grupo_id = partida->grupo_id;

            // Eliminar de la lista
            *pp = partida->next;

            pthread_mutex_unlock(&partidas_mutex);

            // Notificar a todos los jugadores
            char mensaje_fin[512];
            snprintf(mensaje_fin, sizeof(mensaje_fin), "GAMEOVER/%s", resultado);
            broadcast_to_group(grupo_id, mensaje_fin);

            // Liberar memoria
            free(partida);

            return 0; // Éxito
        }
        pp = &(*pp)->next;
    }

    pthread_mutex_unlock(&partidas_mutex);
    return -1; // Partida no encontrada
}

// Función para procesar comandos relacionados con partidas y turnos
int procesar_comando_partida(int codigo, const char *usuario, const char *contrasena,
                             const char *mensaje, char *respuesta, int respuesta_size)
{
    // Comando para iniciar partida (solo el líder puede hacerlo)
    if (codigo == 20)
    {
        // Verificar que el usuario es líder de un grupo
        int es_lider = 0;
        int grupo_id = obtener_grupo_id(usuario);

        if (grupo_id > 0)
        {
            // Verificar si es el líder (implementación depende de tu sistema)
            es_lider = es_lider_grupo(usuario, grupo_id);
        }

        if (es_lider)
        {
            // Crear la partida
            int partida_id = crear_partida(grupo_id);

            if (partida_id > 0)
            {
                // Iniciar la partida (asignar turnos aleatorios)
                int result = iniciar_partida(partida_id);

                if (result == 0)
                {
                    strncpy(respuesta, "GAMESTART_OK/Partida iniciada correctamente", respuesta_size);
                }
                else
                {
                    snprintf(respuesta, respuesta_size, "ERROR/No se pudo iniciar la partida (código %d)", result);
                }
            }
            else
            {
                snprintf(respuesta, respuesta_size, "ERROR/No se pudo crear la partida (código %d)", partida_id);
            }
        }
        else
        {
            strncpy(respuesta, "ERROR/Solo el líder del grupo puede iniciar la partida", respuesta_size);
        }

        return 1; // Comando manejado
    }
    // Comando para realizar una acción en el juego
    else if (codigo == 21)
    {
        // Formato esperado: 21/usuario/accion/datos
        char accion[50] = {0};
        char datos[800] = {0};

        // El campo 'contrasena' contiene la acción
        strncpy(accion, contrasena, sizeof(accion) - 1);

        // El campo 'mensaje' contiene los datos adicionales
        if (mensaje != NULL)
        {
            strncpy(datos, mensaje, sizeof(datos) - 1);
        }

        // Verificar que es el turno del jugador
        if (es_turno_de_jugador(usuario))
        {
            // Obtener la partida del jugador
            GameInfo *partida = obtener_partida_por_jugador(usuario);

            if (partida != NULL)
            {
                // Formato del mensaje: ACTION/jugador/accion/datos
                char mensaje_accion[1024];
                snprintf(mensaje_accion, sizeof(mensaje_accion), "ACTION/%s/%s/%s",
                         usuario, accion, datos);

                // Notificar a todos los jugadores de la partida
                broadcast_to_group(partida->grupo_id, mensaje_accion);

                // CAMBIO IMPORTANTE: Avanzar automáticamente al siguiente turno
                // después de procesar la acción
                int result = avanzar_turno(partida->partida_id);

                if (result == 0)
                {
                    strncpy(respuesta, "ACTION_OK/Acción procesada", respuesta_size);
                }
                else
                {
                    snprintf(respuesta, respuesta_size,
                             "ERROR/Acción procesada pero no se pudo avanzar el turno (código %d)",
                             result);
                }
            }
            else
            {
                strncpy(respuesta, "ERROR/No estás en una partida activa", respuesta_size);
            }
        }
        else
        {
            strncpy(respuesta, "ERROR/No es tu turno para realizar acciones", respuesta_size);
        }

        return 1; // Comando manejado
    }
    // Comando para pasar turno
    else if (codigo == 22)
    {
        // Verificar que es el turno del jugador
        if (es_turno_de_jugador(usuario))
        {
            // Obtener la partida del jugador
            GameInfo *partida = obtener_partida_por_jugador(usuario);

            if (partida != NULL)
            {
                // Avanzar al siguiente turno
                int result = avanzar_turno(partida->partida_id);

                if (result == 0)
                {
                    strncpy(respuesta, "TURN_OK/Turno pasado correctamente", respuesta_size);
                }
                else
                {
                    snprintf(respuesta, respuesta_size, "ERROR/No se pudo avanzar el turno (código %d)", result);
                }
            }
            else
            {
                strncpy(respuesta, "ERROR/No estás en una partida activa", respuesta_size);
            }
        }
        else
        {
            strncpy(respuesta, "ERROR/No es tu turno para pasar", respuesta_size);
        }

        return 1; // Comando manejado
    }

    return 0; // Comando no manejado
}

// Función auxiliar para verificar si un usuario es líder de un grupo
int es_lider_grupo(const char *usuario, int grupo_id)
{
    // Implementar esta función según tu sistema de grupos
    // Por ejemplo, podrías verificar si el usuario es el primero en la lista de jugadores del grupo

    // Por ahora, usa una función externa declarada en server.c
    extern int es_lider_grupo(const char *usuario, int grupo_id);
    return es_lider_grupo(usuario, grupo_id);
}