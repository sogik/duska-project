#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <time.h>
#include "partidas.h"
#include <unistd.h>
#include "server.h"
#include "basedatos.h"

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

    // **PREPARAR ARRAY PARA LA BASE DE DATOS**
    char jugadores_bd[10][50];

    // Copiar nombres de jugadores
    for (int i = 0; i < num_jug; i++)
    {
        char *nombre = obtener_usuario_grupo(partida->grupo_id, i);
        if (nombre)
        {
            partida->jugadores[i] = strdup(nombre);

            // **ASEGURAR QUE SE COPIE CORRECTAMENTE PARA LA BD**
            strncpy(jugadores_bd[i], nombre, sizeof(jugadores_bd[i]) - 1);
            jugadores_bd[i][sizeof(jugadores_bd[i]) - 1] = '\0';

            printf("[PARTIDA] Jugador %d: '%s' preparado para BD\n", i, jugadores_bd[i]);
        }
        else
        {
            partida->jugadores[i] = NULL;
            jugadores_bd[i][0] = '\0';
        }
    }

    // **GUARDAR EN LA BASE DE DATOS CON JUGADORES**
    MYSQL *conn = mysql_init(NULL);
    if (mysql_real_connect(conn, "localhost", "duska_user", "tu_contraseña", "duska_project", 0, NULL, 0))
    {
        int partida_bd_id = insertarPartida(conn, num_jug, jugadores_bd);
        if (partida_bd_id > 0)
        {
            partida->partida_bd_id = partida_bd_id;
            printf("[PARTIDA] Guardada en BD con ID: %d, jugadores: ", partida_bd_id);
            for (int i = 0; i < num_jug; i++)
            {
                printf("%s%s", jugadores_bd[i], (i < num_jug - 1) ? "," : "");
            }
            printf("\n");
        }
        mysql_close(conn);
    }
    else
    {
        printf("[ERROR] No se pudo conectar a la BD para guardar partida\n");
    }

    // Asignar turno inicial aleatorio
    srand(time(NULL));
    partida->turno_actual = rand() % num_jug;

    partida->ronda_actual = 0;
    partida->total_rondas = 4;
    generar_carta_para_ronda_actual(partida);

    // **ACTUALIZAR RONDA EN BD**
    if (conn && partida->partida_bd_id > 0)
    {
        conn = mysql_init(NULL);
        if (mysql_real_connect(conn, "localhost", "duska_user", "tu_contraseña", "duska_project", 0, NULL, 0))
        {
            /*actualizarRondaPartida(conn, partida->partida_bd_id,
                                   partida->ronda_actual + 1,
                                   partida->cartas_ronda[partida->ronda_actual]);*/
            mysql_close(conn);
        }
    }

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
    snprintf(mensaje, sizeof(mensaje), "GAMESTART/OK\n");
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
int eliminar_jugador_de_partida(GameInfo *partida, const char *jugador_eliminado)
{
    if (!partida || !jugador_eliminado)
    {
        printf("[ERROR] Parámetros nulos en eliminar_jugador_de_partida\n");
        return -1;
    }

    printf("[ELIMINACIÓN] ===== ELIMINANDO JUGADOR =====\n");
    printf("[ELIMINACIÓN] Jugador: %s\n", jugador_eliminado);
    printf("[ELIMINACIÓN] Partida ID: %d\n", partida->partida_id);

    // **BUSCAR Y MARCAR JUGADOR COMO ELIMINADO**
    int jugador_encontrado = 0;
    int indice_jugador = -1;

    for (int i = 0; i < partida->num_jugadores; i++)
    {
        if (strcmp(partida->jugadores[i], jugador_eliminado) == 0)
        {
            if (partida->jugadores_eliminados[i] == 0) // Solo si no estaba ya eliminado
            {
                partida->jugadores_eliminados[i] = 1;
                partida->num_jugadores_activos--;
                jugador_encontrado = 1;
                indice_jugador = i;
                printf("[ELIMINACIÓN] Jugador %s marcado como eliminado (posición %d)\n", jugador_eliminado, i);
            }
            else
            {
                printf("[ELIMINACIÓN] Jugador %s ya estaba eliminado\n", jugador_eliminado);
                return 0; // No hacer nada más
            }
            break;
        }
    }

    if (!jugador_encontrado)
    {
        printf("[ERROR] Jugador %s no encontrado en la partida\n", jugador_eliminado);
        return -1;
    }

    printf("[ELIMINACIÓN] Jugadores activos restantes: %d\n", partida->num_jugadores_activos);

    // **ENVIAR MENSAJE DE ELIMINACIÓN INMEDIATAMENTE**
    char mensaje_eliminacion[256];
    snprintf(mensaje_eliminacion, sizeof(mensaje_eliminacion), "JUGADOR_ELIMINADO/%s", jugador_eliminado);

    printf("[ELIMINACIÓN] Enviando mensaje: %s\n", mensaje_eliminacion);
    broadcast_to_group(partida->grupo_id, mensaje_eliminacion);

    // **VERIFICAR SI HAY GANADOR (SOLO 1 JUGADOR ACTIVO)**
    if (partida->num_jugadores_activos == 1)
    {
        // **BUSCAR AL GANADOR**
        char *ganador = NULL;
        for (int i = 0; i < partida->num_jugadores; i++)
        {
            if (partida->jugadores_eliminados[i] == 0)
            {
                ganador = partida->jugadores[i];
                printf("[ELIMINACIÓN] Ganador encontrado: %s\n", ganador);
                break;
            }
        }

        if (ganador != NULL)
        {
            // **MARCAR PARTIDA COMO FINALIZADA**
            partida->estado = 2; // Finalizada

            // **ESPERAR UN MOMENTO**
            usleep(1000000); // 1 segundo

            // **ENVIAR MENSAJE DE FIN DE PARTIDA**
            char mensaje_ganador[256];
            snprintf(mensaje_ganador, sizeof(mensaje_ganador), "FIN_PARTIDA/%s", ganador);

            printf("[ELIMINACIÓN] Enviando mensaje de ganador: %s\n", mensaje_ganador);
            broadcast_to_group(partida->grupo_id, mensaje_ganador);

            // **ACTUALIZAR BASE DE DATOS**
            if (partida->partida_bd_id > 0)
            {
                MYSQL *conn = mysql_init(NULL);
                if (mysql_real_connect(conn, "localhost", "duska_user", "tu_contraseña", "duska_project", 0, NULL, 0))
                {
                    actualizarPartidaFinalizada(conn, partida->partida_bd_id, ganador);
                    mysql_close(conn);
                }
            }

            // **ESPERAR ANTES DE DISOLVER GRUPO**
            usleep(2000000); // 2 segundos adicionales

            // **DISOLVER EL GRUPO**
            int grupo_id = partida->grupo_id;
            disolver_grupo(grupo_id);

            printf("[ELIMINACIÓN] Partida finalizada y grupo %d disuelto\n", grupo_id);

            return 1; // Código especial: partida terminada
        }
    }
    else if (partida->num_jugadores_activos > 1)
    {
        // **LA PARTIDA CONTINÚA**
        printf("[ELIMINACIÓN] La partida continúa con %d jugadores\n", partida->num_jugadores_activos);

        // **SI EL ELIMINADO TENÍA EL TURNO, AVANZAR**
        if (partida->turno_actual == indice_jugador)
        {
            printf("[ELIMINACIÓN] El jugador eliminado tenía el turno, avanzando...\n");

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

                // **ESPERAR ANTES DE ENVIAR NUEVO TURNO**
                usleep(1000000); // 1 segundo

                // Notificar el nuevo turno
                char mensaje_turno[100];
                snprintf(mensaje_turno, sizeof(mensaje_turno), "TURN/%s",
                         partida->jugadores[partida->turno_actual]);
                broadcast_to_group(partida->grupo_id, mensaje_turno);

                printf("[TURNO] Nuevo turno asignado a: %s\n",
                       partida->jugadores[partida->turno_actual]);
            }
        }

        // **CONTINUAR A LA SIGUIENTE RONDA**
        partida->ronda_actual++;

        if (partida->ronda_actual >= partida->total_rondas)
        {
            partida->ronda_actual = 0; // Reiniciar ciclo
        }

        // **GENERAR NUEVA CARTA**
        generar_carta_para_ronda_actual(partida);

        // **ENVIAR MENSAJE DE NUEVA RONDA**
        char mensaje_nueva_ronda[256];
        snprintf(mensaje_nueva_ronda, sizeof(mensaje_nueva_ronda),
                 "NUEVA_RONDA/%d/%s",
                 partida->ronda_actual + 1,
                 partida->cartas_ronda[partida->ronda_actual]);

        usleep(500000); // 0.5 segundos

        printf("[ELIMINACIÓN] Enviando nueva ronda: %s\n", mensaje_nueva_ronda);
        broadcast_to_group(partida->grupo_id, mensaje_nueva_ronda);

        return 0; // Éxito, partida continúa
    }
    else
    {
        // **NO QUEDAN JUGADORES ACTIVOS - ERROR**
        printf("[ERROR] No quedan jugadores activos en la partida\n");

        char mensaje_cancelada[100];
        sprintf(mensaje_cancelada, "PARTIDA_CANCELADA/No hay jugadores activos");
        broadcast_to_group(partida->grupo_id, mensaje_cancelada);

        partida->estado = 2;

        int grupo_id = partida->grupo_id;
        usleep(1000000);
        disolver_grupo(grupo_id);

        return 2; // Código especial: partida cancelada
    }

    printf("[ELIMINACIÓN] ===== FIN ELIMINACIÓN =====\n");
    return 0;
}

// Generar las cartas designadas para cada ronda
void generar_carta_para_ronda_actual(GameInfo *partida)
{
    // **DEFINIR SOLO 3 TIPOS DE CARTAS (SIN JOKERS)**
    const char *tipos_cartas[] = {"ACES", "REYES", "REINAS"};
    int num_tipos = 3; // **CAMBIAR DE 4 A 3**

    // Asignar un tipo de carta aleatoriamente (SIN JOKERS)
    int indice_tipo = rand() % num_tipos;

    // Guardar el tipo de carta para esta ronda
    strncpy(partida->cartas_ronda[partida->ronda_actual], tipos_cartas[indice_tipo],
            sizeof(partida->cartas_ronda[partida->ronda_actual]) - 1);

    printf("[RONDA] Ronda %d: Carta designada '%s' (los Jokers son comodín)\n",
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