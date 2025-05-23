#ifndef MESAS_H
#define MESAS_H

#include <pthread.h>

typedef enum {
    MESA_ASES,
    MESA_REINAS,
    MESA_REYES
} TipoMesa;

typedef struct {
    int grupo_id;
    TipoMesa tipo;
} Mesa;

#define MAX_MESAS 50
extern Mesa mesas[MAX_MESAS];
extern int num_mesas;
extern pthread_mutex_t mutex_mesas;

void crear_mesa_para_grupo(int grupo_id);
TipoMesa obtener_tipo_mesa(int grupo_id);
void notificar_mesa_a_grupo(int grupo_id);

#endif
