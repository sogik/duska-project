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

void guardar_ultima_jugada(GameInfo *partida, const char *jugador, char cartas[][10], int num_cartas)
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

    // Marcar al jugador como eliminado SOLO si no estaba ya eliminado
    if (partida->jugadores_eliminados[indice_jugador] == 0)
    {
        partida->jugadores_eliminados[indice_jugador] = 1;
        partida->num_jugadores_activos--;

        printf("[PARTIDA] Jugador %s eliminado de la partida %d. Quedan %d jugadores activos.\n",
               jugador, partida->partida_id, partida->num_jugadores_activos);
    }
    else
    {
        printf("[PARTIDA] Jugador %s ya estaba eliminado\n", jugador);
        return 0; // No hacer nada más si ya estaba eliminado
    }

    // VERIFICAR INMEDIATAMENTE SI LA PARTIDA DEBE TERMINAR
    if (partida->num_jugadores_activos <= 1)
    {
        printf("[PARTIDA] Solo queda %d jugador(es) activo(s). FINALIZANDO PARTIDA.\n",
               partida->num_jugadores_activos);

        // Buscar al ganador (único jugador activo)
        char *ganador = NULL;
        for (int i = 0; i < partida->num_jugadores; i++)
        {
            if (partida->jugadores_eliminados[i] == 0)
            {
                ganador = partida->jugadores[i];
                printf("[PARTIDA] Ganador encontrado: %s\n", ganador);
                break;
            }
        }

        if (ganador != NULL)
        {
            // HAY UN GANADOR - Finalizar partida
            char mensaje_ganador[100];
            sprintf(mensaje_ganador, "FIN_PARTIDA/%s", ganador);
            broadcast_to_group(partida->grupo_id, mensaje_ganador);

            printf("[PARTIDA] Mensaje de fin enviado: %s\n", mensaje_ganador);

            // Marcar partida como finalizada
            partida->estado = 2;

            // Programar disolución del grupo
            int grupo_id = partida->grupo_id;
            usleep(1000000); // 1 segundo para procesamiento
            disolver_grupo(grupo_id);

            printf("[PARTIDA] Partida %d finalizada y grupo %d disuelto\n",
                   partida->partida_id, grupo_id);

            return 1; // Código especial: partida terminada
        }
        else
        {
            // NO HAY GANADOR (caso raro)
            char mensaje_cancelada[100];
            sprintf(mensaje_cancelada, "PARTIDA_CANCELADA/No hay jugadores activos");
            broadcast_to_group(partida->grupo_id, mensaje_cancelada);

            partida->estado = 2;

            int grupo_id = partida->grupo_id;
            usleep(1000000);
            disolver_grupo(grupo_id);

            printf("[PARTIDA] Partida cancelada - No hay jugadores activos\n");
            return 2; // Código especial: partida cancelada
        }
    }

    // SI QUEDAN MÚLTIPLES JUGADORES, CONTINUAR EL JUEGO
    printf("[PARTIDA] La partida continúa con %d jugadores activos\n", partida->num_jugadores_activos);

    // Si el jugador eliminado tenía el turno, avanzar al siguiente jugador activo
    if (partida->turno_actual == indice_jugador)
    {
        printf("[PARTIDA] El jugador eliminado tenía el turno, avanzando...\n");

        // Buscar el siguiente jugador activo
        int nuevo_turno = (indice_jugador + 1) % partida->num_jugadores;
        int intentos = 0;

        while (partida->jugadores_eliminados[nuevo_turno] && intentos < partida->num_jugadores)
        {
            nuevo_turno = (nuevo_turno + 1) % partida->num_jugadores;
            intentos++;
        }

        if (intentos < partida->num_jugadores && !partida->jugadores_eliminados[nuevo_turno])
        {
            partida->turno_actual = nuevo_turno;

            // Notificar el nuevo turno
            char mensaje_turno[100];
            snprintf(mensaje_turno, sizeof(mensaje_turno), "TURN/%s",
                     partida->jugadores[partida->turno_actual]);
            broadcast_to_group(partida->grupo_id, mensaje_turno);

            printf("[TURNO] Nuevo turno asignado a: %s\n",
                   partida->jugadores[partida->turno_actual]);
        }
    }

    // AVANZAR RONDA solo si la partida continúa
    if (partida->num_jugadores_activos > 1)
    {
        partida->ronda_actual++;

        if (partida->ronda_actual >= partida->total_rondas)
        {
            partida->ronda_actual = 0; // Reiniciar ciclo de rondas
        }

        generar_carta_para_ronda_actual(partida);

        printf("[RONDA] Avanzando a ronda %d, carta designada: %s\n",
               partida->ronda_actual + 1,
               partida->cartas_ronda[partida->ronda_actual]);

        char mensaje_ronda[100];
        sprintf(mensaje_ronda, "NUEVA_RONDA/%d",
                partida->ronda_actual + 1);
        broadcast_to_group(partida->grupo_id, mensaje_ronda);
    }

    return 0; // Éxito, partida continúa
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
    // Buscar la partida por ID
    GameInfo *partida = obtener_partida_por_id(partida_id);
    if (!partida)
    {
        printf("[ERROR] No se encontró la partida con ID %d\n", partida_id);
        return -1;
    }

    if (partida->estado != 1)
    {
        printf("[ERROR] La partida %d no está activa (estado: %d)\n", partida_id, partida->estado);
        return -2;
    }

    printf("[TURNO] Avanzando turno en partida %d. Turno actual: %d (%s)\n",
           partida_id, partida->turno_actual, partida->jugadores[partida->turno_actual]);

    // Verificar si quedan suficientes jugadores activos
    if (partida->num_jugadores_activos <= 1)
    {
        printf("[TURNO] Solo queda %d jugador(es) activo(s). Finalizando partida.\n",
               partida->num_jugadores_activos);

        // Buscar al ganador (único jugador activo)
        char *ganador = NULL;
        for (int i = 0; i < partida->num_jugadores; i++)
        {
            if (partida->jugadores_eliminados[i] == 0)
            {
                ganador = partida->jugadores[i];
                break;
            }
        }

        if (ganador != NULL)
        {
            printf("[PARTIDA] Ganador encontrado: %s\n", ganador);

            // Enviar mensaje de fin de partida
            char mensaje_ganador[100];
            sprintf(mensaje_ganador, "FIN_PARTIDA/%s", ganador);
            broadcast_to_group(partida->grupo_id, mensaje_ganador);

            // Marcar partida como finalizada
            partida->estado = 2;

            // Programar disolución del grupo (se hace en el servidor principal)
            return 1; // Código especial para indicar fin de partida
        }
        else
        {
            printf("[ERROR] No se encontró ganador en partida %d\n", partida_id);
            return -3;
        }
    }

    // BUSCAR EL SIGUIENTE JUGADOR ACTIVO
    int turno_original = partida->turno_actual;
    int intentos = 0;

    do
    {
        // Avanzar al siguiente jugador
        partida->turno_actual = (partida->turno_actual + 1) % partida->num_jugadores;
        intentos++;

        printf("[TURNO] Probando jugador %d: %s (eliminado: %s)\n",
               partida->turno_actual,
               partida->jugadores[partida->turno_actual],
               partida->jugadores_eliminados[partida->turno_actual] ? "SÍ" : "NO");

        // Evitar bucle infinito
        if (intentos > partida->num_jugadores)
        {
            printf("[ERROR] No se encontró ningún jugador activo después de %d intentos\n", intentos);
            return -4;
        }

    } while (partida->jugadores_eliminados[partida->turno_actual] == 1);

    // Verificar que encontramos un jugador válido
    if (partida->jugadores_eliminados[partida->turno_actual] == 1)
    {
        printf("[ERROR] El jugador seleccionado %s está eliminado\n",
               partida->jugadores[partida->turno_actual]);
        return -5;
    }

    // Obtener nombre del siguiente jugador ACTIVO
    char *siguiente_jugador = partida->jugadores[partida->turno_actual];
    if (!siguiente_jugador)
    {
        printf("[ERROR] El nombre del siguiente jugador es NULL\n");
        return -6;
    }

    printf("[TURNO] Turno asignado al jugador %d: %s\n",
           partida->turno_actual, siguiente_jugador);

    // Enviar mensaje de turno al grupo
    char mensaje_turno[100];
    snprintf(mensaje_turno, sizeof(mensaje_turno), "TURN/%s", siguiente_jugador);

    printf("[TURNO] Enviando mensaje de turno: '%s'\n", mensaje_turno);

    int resultado_broadcast = broadcast_to_group(partida->grupo_id, mensaje_turno);
    if (resultado_broadcast < 0)
    {
        printf("[ERROR] Error al enviar mensaje de turno al grupo %d\n", partida->grupo_id);
        return -7;
    }

    printf("[TURNO] Turno avanzado exitosamente de %s a %s\n",
           partida->jugadores[turno_original], siguiente_jugador);

    return 0; // Éxito
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