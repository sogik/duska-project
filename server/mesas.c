#include "mesas.h"
#include "conexiones.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>
#include "server.h"

Mesa mesas[MAX_MESAS];
int num_mesas = 0;
pthread_mutex_t mutex_mesas = PTHREAD_MUTEX_INITIALIZER;

void crear_mesa_para_grupo(int grupo_id)
{
    pthread_mutex_lock(&mutex_mesas);
    if (num_mesas < MAX_MESAS)
    {
        mesas[num_mesas].grupo_id = grupo_id;
        int r = rand() % 3;
        if (r == 0)
            mesas[num_mesas].tipo = MESA_ASES;
        else if (r == 1)
            mesas[num_mesas].tipo = MESA_REINAS;
        else
            mesas[num_mesas].tipo = MESA_REYES;
        num_mesas++;
    }
    pthread_mutex_unlock(&mutex_mesas);
}

TipoMesa obtener_tipo_mesa(int grupo_id)
{
    TipoMesa tipo = MESA_ASES;
    pthread_mutex_lock(&mutex_mesas);
    for (int i = 0; i < num_mesas; i++)
    {
        if (mesas[i].grupo_id == grupo_id)
        {
            tipo = mesas[i].tipo;
            break;
        }
    }
    pthread_mutex_unlock(&mutex_mesas);
    return tipo;
}

void notificar_mesa_a_grupo(int grupo_id)
{
    TipoMesa tipo = obtener_tipo_mesa(grupo_id);
    char mensaje[64];
    if (tipo == MESA_ASES)
        strcpy(mensaje, "MESA/ASES");
    else if (tipo == MESA_REINAS)
        strcpy(mensaje, "MESA/REINAS");
    else
        strcpy(mensaje, "MESA/REYES");
    broadcast_to_group(grupo_id, mensaje);
}
