#ifndef SISTEMA_INVITACIONES_H
#define SISTEMA_INVITACIONES_H

#include <mysql.h>

void enviar_invitacion(MYSQL *conn, int invitador_id, const char* invitado_nombre);
void actualizar_estado_invitacion(MYSQL *conn, int invitacion_id, const char* estado);

#endif
