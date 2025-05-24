#include "gamelogic.h"
#include "conexiones.h"
#include <string.h>
#include <stdlib.h>
#include <stdio.h>
#include "server.h"

/*void iniciar_partida(Jugador jugadores[MAX_JUGADORES]) {
    for(int i = 0; i < MAX_JUGADORES; i++) {
        for(int j = 0; j < CARTAS_POR_JUGADOR; j++) {
            if(i == 0 && j == 0) {
                jugadores[i].mano[j].tipo = CARD_JOKER;
                jugadores[i].mano[j].valor = 0;
            } else {
                jugadores[i].mano[j].tipo = (rand() % 3); // as, reina o rey
                jugadores[i].mano[j].valor = (rand() % 10) + 1;
            }
        }
        jugadores[i].en_partida = true;
    }
}*/

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

int comprobar_verdad(int grupo_id, int jugador_que_reta, int jugador_retentado)
{
    int eliminado = -1;
    pthread_mutex_lock(&mutex_mesas);
    for (int i = 0; i < num_mesas; i++)
    {
        if (mesas[i].grupo_id == grupo_id)
        {
            TipoMesa tipoMesa = mesas[i].tipo;
            Carta carta = mesas[i].ultima_jugada;
            bool mintio = false;
            if ((tipoMesa == MESA_ASES && carta.tipo != CARD_AS) ||
                (tipoMesa == MESA_REINAS && carta.tipo != CARD_REINA) ||
                (tipoMesa == MESA_REYES && carta.tipo != CARD_REY))
            {
                mintio = true;
            }

            if (mintio)
            {
                // El jugador retado mintió: eliminado
                mesas[i].jugadores_vivos[jugador_retentado] = false;
                eliminado = jugador_retentado;
            }
            else
            {
                // El retador se equivocó: eliminado
                mesas[i].jugadores_vivos[jugador_que_reta] = false;
                eliminado = jugador_que_reta;
            }
            break;
        }
    }
    pthread_mutex_unlock(&mutex_mesas);
    return eliminado;
}

void limpiar_mesa(int grupo_id)
{
    pthread_mutex_lock(&mutex_mesas);
    for (int i = 0; i < num_mesas; i++)
    {
        if (mesas[i].grupo_id == grupo_id)
        {
            memset(&mesas[i], 0, sizeof(Mesa));
            mesas[i].grupo_id = -1;
            break;
        }
    }
    pthread_mutex_unlock(&mutex_mesas);
}

void notificar_estado_grupo(int grupo_id)
{
    char estado[256] = "ESTADO/";
    pthread_mutex_lock(&mutex_mesas);
    for (int i = 0; i < num_mesas; i++)
    {
        if (mesas[i].grupo_id == grupo_id)
        {
            for (int j = 0; j < MAX_JUGADORES; j++)
            {
                char jugador_info[32];
                snprintf(jugador_info, sizeof(jugador_info), "%d:%d:",
                         j, mesas[i].jugadores_vivos[j]);
                strcat(estado, jugador_info);
            }
            break;
        }
    }
    pthread_mutex_unlock(&mutex_mesas);
    broadcast_to_group(grupo_id, estado);
}

void expulsar_jugador(int grupo_id, int jugador_id)
{
    pthread_mutex_lock(&mutex_mesas);
    for (int i = 0; i < num_mesas; i++)
    {
        if (mesas[i].grupo_id == grupo_id)
        {
            mesas[i].jugadores_vivos[jugador_id] = false;
            break;
        }
    }
    pthread_mutex_unlock(&mutex_mesas);
}

// Estructura para almacenar la última jugada completa
typedef struct {
    int tipos_cartas[10];
    int cantidad;
    int jugador_id;
    int grupo_id;
} UltimaJugada;

static UltimaJugada ultima_jugada_global;

// Registrar jugada múltiple
int registrar_jugada_multiple(int grupo_id, int jugador_id, int tipos_cartas[], int cantidad) {
    if (grupo_id <= 0 || jugador_id < 0 || cantidad <= 0 || cantidad > 10) {
        return 0;
    }
    
    pthread_mutex_lock(&mutex_mesas);
    
    // Buscar la mesa
    int indice_mesa = -1;
    for (int i = 0; i < num_mesas; i++) {
        if (mesas[i].grupo_id == grupo_id) {
            indice_mesa = i;
            break;
        }
    }
    
    if (indice_mesa == -1) {
        pthread_mutex_unlock(&mutex_mesas);
        return 0;
    }
    
    // Actualizar mesa con la nueva jugada
    mesas[indice_mesa].jugador_ultimo = jugador_id;
    
    // Guardar la jugada completa
    ultima_jugada_global.grupo_id = grupo_id;
    ultima_jugada_global.jugador_id = jugador_id;
    ultima_jugada_global.cantidad = cantidad;
    
    for (int i = 0; i < cantidad; i++) {
        ultima_jugada_global.tipos_cartas[i] = tipos_cartas[i];
    }
    
    printf("[JUEGO] Registrada jugada: %d cartas del jugador %d en grupo %d\n", 
           cantidad, jugador_id, grupo_id);
    
    pthread_mutex_unlock(&mutex_mesas);
    return 1;
}

// Verificar mentira en la última jugada
int comprobar_mentira_ultima_jugada(int grupo_id, int jugador_acusador) {
    if (grupo_id <= 0) return 0;
    
    pthread_mutex_lock(&mutex_mesas);
    
    // Verificar que tenemos una jugada para revisar
    if (ultima_jugada_global.grupo_id != grupo_id || ultima_jugada_global.cantidad == 0) {
        printf("[JUEGO] Error: No hay jugada previa para verificar\n");
        pthread_mutex_unlock(&mutex_mesas);
        return 0;
    }
    
    // Buscar la mesa
    int indice_mesa = -1;
    for (int i = 0; i < num_mesas; i++) {
        if (mesas[i].grupo_id == grupo_id) {
            indice_mesa = i;
            break;
        }
    }
    
    if (indice_mesa == -1) {
        pthread_mutex_unlock(&mutex_mesas);
        return 0;
    }
    
    // Determinar el tipo de carta requerido
    TipoMesa tipo_mesa = mesas[indice_mesa].tipo;
    TipoCarta carta_requerida;
    
    switch (tipo_mesa) {
        case MESA_ASES:
            carta_requerida = CARD_AS;
            break;
        case MESA_REINAS:
            carta_requerida = CARD_REINA;
            break;
        case MESA_REYES:
            carta_requerida = CARD_REY;
            break;
        default:
            carta_requerida = CARD_AS;
    }
    
    // Verificar si TODAS las cartas son del tipo correcto
    int todas_correctas = 1;
    for (int i = 0; i < ultima_jugada_global.cantidad; i++) {
        if (ultima_jugada_global.tipos_cartas[i] != carta_requerida) {
            todas_correctas = 0;
            break;
        }
    }
    
    int resultado;
    int jugador_acusado = ultima_jugada_global.jugador_id;
    
    if (!todas_correctas) {
        // El jugador mintió
        printf("[JUEGO] ¡MENTIRA DETECTADA! Jugador %d mintió\n", jugador_acusado);
        mesas[indice_mesa].jugadores_vivos[jugador_acusado] = false;
        resultado = 1; // Mentiroso detectado
    } else {
        // El jugador dijo la verdad
        printf("[JUEGO] ¡VERDAD! Jugador %d dijo la verdad\n", jugador_acusado);
        mesas[indice_mesa].jugadores_vivos[jugador_acusador] = false;
        resultado = 2; // Era verdad
    }
    
    // Limpiar la jugada después de verificarla
    ultima_jugada_global.cantidad = 0;
    
    pthread_mutex_unlock(&mutex_mesas);
    return resultado;
}
