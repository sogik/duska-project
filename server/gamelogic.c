#include "gamelogic.h"
#include "conexiones.h"
#include <string.h>
#include <stdlib.h>
#include <stdio.h>
#include "server.h"

// Declare externa para acceder a las mesas desde gamelogic.c
extern Mesa mesas[MAX_MESAS];
extern int num_mesas;
extern pthread_mutex_t mutex_mesas;

// Mapeo de índices de jugadores a nombres de usuario
// Estructura simple para simplificar el ejemplo
typedef struct
{
    char nombre[50];
    bool activo;
    TipoCarta cartas[10]; // Máximo 10 cartas por jugador
    int num_cartas;
} JugadorInfo;

// Array asociativo de jugadores por mesa
JugadorInfo jugadores_por_mesa[MAX_MESAS][MAX_JUGADORES_MESA];

// Implementación de las funciones existentes
int obtener_ganador(int grupo_id)
{
    pthread_mutex_lock(&mutex_mesas);
    int ganador = -1;
    int vivos = 0;
    for (int i = 0; i < num_mesas; i++)
    {
        if (mesas[i].grupo_id == grupo_id)
        {
            for (int j = 0; j < MAX_JUGADORES; j++)
            {
                if (mesas[i].jugadores_vivos[j])
                {
                    ganador = j;
                    vivos++;
                }
            }
            break;
        }
    }
    pthread_mutex_unlock(&mutex_mesas);
    return (vivos == 1) ? ganador : -1;
}

void manejar_jugada(int grupo_id, int jugador_id, Carta carta_jugada)
{
    pthread_mutex_lock(&mutex_mesas);
    for (int i = 0; i < num_mesas; i++)
    {
        if (mesas[i].grupo_id == grupo_id)
        {
            mesas[i].ultima_jugada = carta_jugada;
            mesas[i].jugador_ultimo = jugador_id;
            break;
        }
    }
    pthread_mutex_unlock(&mutex_mesas);
}

// Implementación de las nuevas funciones
int obtener_indice_jugador(int grupo_id, const char *nombre_usuario)
{
    pthread_mutex_lock(&mutex_mesas);

    int mesa_idx = -1;
    for (int i = 0; i < num_mesas; i++)
    {
        if (mesas[i].grupo_id == grupo_id)
        {
            mesa_idx = i;
            break;
        }
    }

    if (mesa_idx == -1)
    {
        pthread_mutex_unlock(&mutex_mesas);
        return -1;
    }

    // Buscar el jugador por nombre
    for (int i = 0; i < MAX_JUGADORES_MESA; i++)
    {
        if (jugadores_por_mesa[mesa_idx][i].activo &&
            strcmp(jugadores_por_mesa[mesa_idx][i].nombre, nombre_usuario) == 0)
        {
            pthread_mutex_unlock(&mutex_mesas);
            return i;
        }
    }

    pthread_mutex_unlock(&mutex_mesas);
    return -1;
}

void obtener_nombre_usuario(int grupo_id, int indice, char *nombre)
{
    pthread_mutex_lock(&mutex_mesas);

    int mesa_idx = -1;
    for (int i = 0; i < num_mesas; i++)
    {
        if (mesas[i].grupo_id == grupo_id)
        {
            mesa_idx = i;
            break;
        }
    }

    if (mesa_idx == -1 || indice < 0 || indice >= MAX_JUGADORES_MESA ||
        !jugadores_por_mesa[mesa_idx][indice].activo)
    {
        strcpy(nombre, "Desconocido");
    }
    else
    {
        strcpy(nombre, jugadores_por_mesa[mesa_idx][indice].nombre);
    }

    pthread_mutex_unlock(&mutex_mesas);
}

void inicializar_juego_mentiroso(int grupo_id)
{
    pthread_mutex_lock(&mutex_mesas);

    int mesa_idx = -1;
    for (int i = 0; i < num_mesas; i++)
    {
        if (mesas[i].grupo_id == grupo_id)
        {
            mesa_idx = i;
            break;
        }
    }

    if (mesa_idx == -1)
    {
        pthread_mutex_unlock(&mutex_mesas);
        return;
    }

    // Inicializar la mesa
    mesas[mesa_idx].tipo = MESA_ASES;
    mesas[mesa_idx].jugador_actual = 0;
    mesas[mesa_idx].jugador_ultimo = -1;

    // Inicializar jugadores vivos
    for (int i = 0; i < MAX_JUGADORES_MESA; i++)
    {
        mesas[mesa_idx].jugadores_vivos[i] = jugadores_por_mesa[mesa_idx][i].activo;
    }

    pthread_mutex_unlock(&mutex_mesas);

    // Repartir cartas
    repartir_cartas_iniciales(grupo_id);

    // Notificar a todos
    notificar_turno_actual(grupo_id);
}

void repartir_cartas_iniciales(int grupo_id)
{
    pthread_mutex_lock(&mutex_mesas);

    int mesa_idx = -1;
    for (int i = 0; i < num_mesas; i++)
    {
        if (mesas[i].grupo_id == grupo_id)
        {
            mesa_idx = i;
            break;
        }
    }

    if (mesa_idx == -1)
    {
        pthread_mutex_unlock(&mutex_mesas);
        return;
    }

    // Crear mazo inicial (simplificado)
    TipoCarta mazo[] = {
        CARD_AS, CARD_AS, CARD_AS, CARD_AS,
        CARD_REY, CARD_REY, CARD_REY, CARD_REY,
        CARD_REINA, CARD_REINA, CARD_REINA, CARD_REINA,
        CARD_JOKER, CARD_JOKER, CARD_JOKER, CARD_JOKER};
    int num_cartas_mazo = sizeof(mazo) / sizeof(mazo[0]);

    // Barajar el mazo (algoritmo Fisher-Yates)
    srand(time(NULL));
    for (int i = num_cartas_mazo - 1; i > 0; i--)
    {
        int j = rand() % (i + 1);
        TipoCarta temp = mazo[i];
        mazo[i] = mazo[j];
        mazo[j] = temp;
    }

    // Contar jugadores activos
    int num_jugadores_activos = 0;
    for (int i = 0; i < MAX_JUGADORES_MESA; i++)
    {
        if (jugadores_por_mesa[mesa_idx][i].activo)
        {
            num_jugadores_activos++;
        }
    }

    // Repartir 5 cartas a cada jugador activo
    int carta_idx = 0;
    for (int i = 0; i < MAX_JUGADORES_MESA && carta_idx < num_cartas_mazo; i++)
    {
        if (jugadores_por_mesa[mesa_idx][i].activo)
        {
            jugadores_por_mesa[mesa_idx][i].num_cartas = 0;

            for (int j = 0; j < CARTAS_POR_JUGADOR && carta_idx < num_cartas_mazo; j++)
            {
                jugadores_por_mesa[mesa_idx][i].cartas[j] = mazo[carta_idx++];
                jugadores_por_mesa[mesa_idx][i].num_cartas++;
            }

            // Notificar al jugador sobre sus cartas
            char nombre_jugador[50];
            strcpy(nombre_jugador, jugadores_por_mesa[mesa_idx][i].nombre);

            char mensaje[200];
            char cartas_str[100] = "";

            for (int j = 0; j < jugadores_por_mesa[mesa_idx][i].num_cartas; j++)
            {
                if (j > 0)
                    strcat(cartas_str, ",");

                switch (jugadores_por_mesa[mesa_idx][i].cartas[j])
                {
                case CARD_AS:
                    strcat(cartas_str, "ace");
                    break;
                case CARD_REY:
                    strcat(cartas_str, "king");
                    break;
                case CARD_REINA:
                    strcat(cartas_str, "queen");
                    break;
                case CARD_JOKER:
                    strcat(cartas_str, "jack");
                    break;
                }
            }

            snprintf(mensaje, sizeof(mensaje), "CARTAS_INICIALES/%s", cartas_str);
            enviar_mensaje_a_usuario(nombre_jugador, mensaje);
        }
    }

    pthread_mutex_unlock(&mutex_mesas);
}

void procesar_jugada_mentiroso(int grupo_id, const char *jugador, const char *cartas_str, const char *tipo_declarado)
{
    pthread_mutex_lock(&mutex_mesas);

    int mesa_idx = -1;
    for (int i = 0; i < num_mesas; i++)
    {
        if (mesas[i].grupo_id == grupo_id)
        {
            mesa_idx = i;
            break;
        }
    }

    if (mesa_idx == -1)
    {
        pthread_mutex_unlock(&mutex_mesas);
        return;
    }

    // Obtener índice del jugador
    int jugador_idx = obtener_indice_jugador(grupo_id, jugador);

    if (jugador_idx == -1 || jugador_idx != mesas[mesa_idx].jugador_actual)
    {
        // No es el turno del jugador
        pthread_mutex_unlock(&mutex_mesas);
        enviar_mensaje_a_usuario(jugador, "ERROR/No es tu turno");
        return;
    }

    // Contar cartas jugadas
    int num_cartas_jugadas = 0;
    char cartas_copy[100];
    strcpy(cartas_copy, cartas_str);

    char *token = strtok(cartas_copy, ",");
    while (token != NULL)
    {
        num_cartas_jugadas++;
        token = strtok(NULL, ",");
    }

    // Validar que el jugador tiene esas cartas
    // La implementación real verificaría los tipos específicos
    if (num_cartas_jugadas > jugadores_por_mesa[mesa_idx][jugador_idx].num_cartas)
    {
        pthread_mutex_unlock(&mutex_mesas);
        enviar_mensaje_a_usuario(jugador, "ERROR/No tienes suficientes cartas");
        return;
    }

    // Determinar el tipo de carta declarado
    TipoCarta tipo_carta;
    if (strcmp(tipo_declarado, "ace") == 0)
        tipo_carta = CARD_AS;
    else if (strcmp(tipo_declarado, "king") == 0)
        tipo_carta = CARD_REY;
    else if (strcmp(tipo_declarado, "queen") == 0)
        tipo_carta = CARD_REINA;
    else
        tipo_carta = CARD_JOKER;

    // Registrar la jugada
    mesas[mesa_idx].ultima_jugada.tipo = tipo_carta;
    mesas[mesa_idx].ultima_jugada.valor = num_cartas_jugadas;
    mesas[mesa_idx].jugador_ultimo = jugador_idx;

    // Actualizar las cartas del jugador (simplificado)
    jugadores_por_mesa[mesa_idx][jugador_idx].num_cartas -= num_cartas_jugadas;

    // Guardar la información real de las cartas jugadas para verificar mentiras posteriormente
    // (Esta implementación es simplificada)

    pthread_mutex_unlock(&mutex_mesas);

    // Notificar a todos los jugadores
    char mensaje[200];
    snprintf(mensaje, sizeof(mensaje), "JUGADA/%s/%d/%s", jugador, num_cartas_jugadas, tipo_declarado);
    broadcast_to_group(grupo_id, mensaje);

    // Avanzar turno
    avanzar_turno(grupo_id);

    // Notificar el nuevo turno
    notificar_turno_actual(grupo_id);
}

int procesar_acusacion(int grupo_id, const char *acusador, const char *acusado)
{
    pthread_mutex_lock(&mutex_mesas);

    int mesa_idx = -1;
    for (int i = 0; i < num_mesas; i++)
    {
        if (mesas[i].grupo_id == grupo_id)
        {
            mesa_idx = i;
            break;
        }
    }

    if (mesa_idx == -1)
    {
        pthread_mutex_unlock(&mutex_mesas);
        return -1;
    }

    // Obtener índices de los jugadores
    int idx_acusador = obtener_indice_jugador(grupo_id, acusador);
    int idx_acusado = obtener_indice_jugador(grupo_id, acusado);

    if (idx_acusador == -1 || idx_acusado == -1)
    {
        pthread_mutex_unlock(&mutex_mesas);
        return -1;
    }

    // Verificar que el acusador puede acusar
    if (idx_acusador != mesas[mesa_idx].jugador_actual)
    {
        pthread_mutex_unlock(&mutex_mesas);
        enviar_mensaje_a_usuario(acusador, "ERROR/No puedes acusar en este momento");
        return -1;
    }

    // Verificar que el acusado fue el último en jugar
    if (idx_acusado != mesas[mesa_idx].jugador_ultimo)
    {
        pthread_mutex_unlock(&mutex_mesas);
        enviar_mensaje_a_usuario(acusador, "ERROR/No puedes acusar a este jugador");
        return -1;
    }

    // Comprobar si mintió (simplificado para este ejemplo)
    // En una implementación real, verificarías las cartas jugadas reales
    TipoMesa tipo_mesa = mesas[mesa_idx].tipo;
    TipoCarta tipo_esperado;

    if (tipo_mesa == MESA_ASES)
        tipo_esperado = CARD_AS;
    else if (tipo_mesa == MESA_REINAS)
        tipo_esperado = CARD_REINA;
    else
        tipo_esperado = CARD_REY;

    // Simulación de resultado (50% de probabilidad de mentira)
    srand(time(NULL));
    int mintio = rand() % 2;

    // En una implementación real, comparas las cartas reales jugadas con lo declarado

    // Determinar quién es eliminado
    int idx_eliminado = mintio ? idx_acusado : idx_acusador;
    mesas[mesa_idx].jugadores_vivos[idx_eliminado] = false;
    jugadores_por_mesa[mesa_idx][idx_eliminado].activo = false;

    pthread_mutex_unlock(&mutex_mesas);

    // Construir mensaje con cartas reveladas (simplificado)
    char tipo_carta_str[20];
    if (mesas[mesa_idx].ultima_jugada.tipo == CARD_AS)
        strcpy(tipo_carta_str, "ace");
    else if (mesas[mesa_idx].ultima_jugada.tipo == CARD_REINA)
        strcpy(tipo_carta_str, "queen");
    else if (mesas[mesa_idx].ultima_jugada.tipo == CARD_REY)
        strcpy(tipo_carta_str, "king");
    else
        strcpy(tipo_carta_str, "jack");

    // Construir lista de cartas reveladas
    char lista_cartas[100] = "";
    for (int i = 0; i < mesas[mesa_idx].ultima_jugada.valor; i++)
    {
        if (i > 0)
            strcat(lista_cartas, ",");
        strcat(lista_cartas, tipo_carta_str);
    }

    // Notificar sobre la revelación
    char mensaje_revelacion[200];
    snprintf(mensaje_revelacion, sizeof(mensaje_revelacion), "REVELACION/%d/%s", !mintio, lista_cartas);
    broadcast_to_group(grupo_id, mensaje_revelacion);

    // Notificar sobre la eliminación
    char mensaje_eliminado[100];
    snprintf(mensaje_eliminado, sizeof(mensaje_eliminado), "ELIMINADO/%s", mintio ? acusado : acusador);
    broadcast_to_group(grupo_id, mensaje_eliminado);

    // Cambiar tipo de ronda
    cambiar_tipo_mesa(grupo_id);

    // Verificar ganador
    int ganador = obtener_ganador(grupo_id);
    if (ganador != -1)
    {
        char nombre_ganador[50];
        obtener_nombre_usuario(grupo_id, ganador, nombre_ganador);

        char mensaje_ganador[100];
        snprintf(mensaje_ganador, sizeof(mensaje_ganador), "GANADOR/%s", nombre_ganador);
        broadcast_to_group(grupo_id, mensaje_ganador);
    }

    // Reiniciar turno
    mesas[mesa_idx].jugador_actual = 0;
    for (int i = 0; i < MAX_JUGADORES_MESA; i++)
    {
        if (mesas[mesa_idx].jugadores_vivos[i])
        {
            mesas[mesa_idx].jugador_actual = i;
            break;
        }
    }

    // Notificar el nuevo turno
    notificar_turno_actual(grupo_id);

    return mintio;
}

void avanzar_turno(int grupo_id)
{
    pthread_mutex_lock(&mutex_mesas);

    int mesa_idx = -1;
    for (int i = 0; i < num_mesas; i++)
    {
        if (mesas[i].grupo_id == grupo_id)
        {
            mesa_idx = i;
            break;
        }
    }

    if (mesa_idx == -1)
    {
        pthread_mutex_unlock(&mutex_mesas);
        return;
    }

    // Obtener índice de jugador actual
    int idx_actual = mesas[mesa_idx].jugador_actual;

    // Buscar el siguiente jugador vivo
    for (int i = 1; i <= MAX_JUGADORES_MESA; i++)
    {
        int siguiente = (idx_actual + i) % MAX_JUGADORES_MESA;
        if (mesas[mesa_idx].jugadores_vivos[siguiente])
        {
            mesas[mesa_idx].jugador_actual = siguiente;
            break;
        }
    }

    pthread_mutex_unlock(&mutex_mesas);
}

void notificar_turno_actual(int grupo_id)
{
    pthread_mutex_lock(&mutex_mesas);

    int mesa_idx = -1;
    for (int i = 0; i < num_mesas; i++)
    {
        if (mesas[i].grupo_id == grupo_id)
        {
            mesa_idx = i;
            break;
        }
    }

    if (mesa_idx == -1)
    {
        pthread_mutex_unlock(&mutex_mesas);
        return;
    }

    // Obtener el jugador actual
    int jugador_actual = mesas[mesa_idx].jugador_actual;

    // Obtener el nombre de usuario del jugador actual
    char nombre_jugador[50];
    obtener_nombre_usuario(grupo_id, jugador_actual, nombre_jugador);

    // Obtener tipo de carta para esta ronda
    TipoMesa tipo_mesa = mesas[mesa_idx].tipo;
    char tipo_str[20];

    if (tipo_mesa == MESA_ASES)
        strcpy(tipo_str, "ace");
    else if (tipo_mesa == MESA_REINAS)
        strcpy(tipo_str, "queen");
    else
        strcpy(tipo_str, "king");

    pthread_mutex_unlock(&mutex_mesas);

    // Enviar información de turno a todos los jugadores
    char mensaje[100];
    snprintf(mensaje, sizeof(mensaje), "TURNO/%s/%s", nombre_jugador, tipo_str);
    broadcast_to_group(grupo_id, mensaje);
}

void cambiar_tipo_mesa(int grupo_id)
{
    pthread_mutex_lock(&mutex_mesas);

    for (int i = 0; i < num_mesas; i++)
    {
        if (mesas[i].grupo_id == grupo_id)
        {
            // Rotar tipo de mesa: ASES -> REINAS -> REYES -> ASES
            if (mesas[i].tipo == MESA_ASES)
                mesas[i].tipo = MESA_REINAS;
            else if (mesas[i].tipo == MESA_REINAS)
                mesas[i].tipo = MESA_REYES;
            else
                mesas[i].tipo = MESA_ASES;

            // Notificar a todos del cambio
            char mensaje[100];
            char tipo_str[20];

            if (mesas[i].tipo == MESA_ASES)
                strcpy(tipo_str, "ace");
            else if (mesas[i].tipo == MESA_REINAS)
                strcpy(tipo_str, "queen");
            else
                strcpy(tipo_str, "king");

            pthread_mutex_unlock(&mutex_mesas);

            snprintf(mensaje, sizeof(mensaje), "NUEVA_RONDA/%s", tipo_str);
            broadcast_to_group(grupo_id, mensaje);

            return;
        }
    }

    pthread_mutex_unlock(&mutex_mesas);
}
