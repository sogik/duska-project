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

void manejar_jugada(int grupo_id, int jugador_id, Carta cartas_jugadas[], int num_cartas, TipoCarta tipo_declarado)
{
    pthread_mutex_lock(&mutex_mesas);
    for (int i = 0; i < num_mesas; i++)
    {
        if (mesas[i].grupo_id == grupo_id)
        {
            mesas[i].jugador_ultimo = jugador_id;
            mesas[i].num_cartas_jugadas = (num_cartas <= MAX_CARTAS_JUGADA) ? num_cartas : MAX_CARTAS_JUGADA;
            mesas[i].tipo_declarado = tipo_declarado;

            // Copiar todas las cartas jugadas
            for (int j = 0; j < mesas[i].num_cartas_jugadas; j++)
            {
                mesas[i].ultima_jugada[j] = cartas_jugadas[j];
            }
            break;
        }
    }
    pthread_mutex_unlock(&mutex_mesas);
}

int comprobar_verdad(int grupo_id, int jugador_que_reta, int jugador_retado)
{
    int eliminado = -1;
    pthread_mutex_lock(&mutex_mesas);
    for (int i = 0; i < num_mesas; i++)
    {
        if (mesas[i].grupo_id == grupo_id)
        {
            TipoMesa tipoMesa = mesas[i].tipo;
            TipoCarta tipo_esperado;

            // Convertir tipo de mesa a tipo de carta esperado
            if (tipoMesa == MESA_ASES)
                tipo_esperado = CARD_AS;
            else if (tipoMesa == MESA_REINAS)
                tipo_esperado = CARD_REINA;
            else
                tipo_esperado = CARD_REY;

            // Verificar si el tipo declarado corresponde al tipo de mesa
            bool mintio = (mesas[i].tipo_declarado != tipo_esperado);

            // Si no mintió en lo declarado, verificar si mintió en las cartas jugadas
            if (!mintio)
            {
                for (int j = 0; j < mesas[i].num_cartas_jugadas; j++)
                {
                    if (mesas[i].ultima_jugada[j].tipo != tipo_esperado)
                    {
                        mintio = true;
                        break;
                    }
                }
            }

            if (mintio)
            {
                // El jugador retado mintió: eliminado
                mesas[i].jugadores_vivos[jugador_retado] = false;
                eliminado = jugador_retado;
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
