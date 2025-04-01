#include <mysql.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "sistemainvitaciones.h"
#include "basedatos.h"

void enviar_invitacion(MYSQL *conn, int invitador_id, const char* invitado_nombre) {
    char query[512];
    MYSQL_RES *res;
    MYSQL_ROW row;

    // Verificar existencia del invitado
    snprintf(query, sizeof(query), 
            "SELECT id_jugador FROM Jugadores WHERE nombre_usuario = '%s'", 
            invitado_nombre);

    if(mysql_query(conn, query) || !(res = mysql_store_result(conn)) || !(row = mysql_fetch_row(res))) {
        printf("Jugador no encontrado\n");
        return;
    }
    int invitado_id = atoi(row[0]);
    mysql_free_result(res);

    // Insertar invitación
    snprintf(query, sizeof(query),
            "INSERT INTO Invitaciones (id_invitador, id_invitado, estado) VALUES (%d, %d, 'pendiente')",
            invitador_id, invitado_id);

    if(mysql_query(conn, query)) {
        printf("Error al crear invitación\n");
        return;
    }

    // Notificaciones
    int invitacion_id = mysql_insert_id(conn);
    int socket_invitado = buscar_socket_por_id(invitado_id);
    int socket_invitador = buscar_socket_por_id(invitador_id);
    
    if(socket_invitado != -1) {
        char mensaje[50];
        snprintf(mensaje, sizeof(mensaje), "INVITACION:%d:%d", invitacion_id, invitador_id);
        send(socket_invitado, mensaje, strlen(mensaje), 0);
    }
    
    if(socket_invitador != -1) {
        char mensaje[50];
        snprintf(mensaje, sizeof(mensaje), "INV_ENVIADA:%d:%d", invitacion_id, invitado_id);
        send(socket_invitador, mensaje, strlen(mensaje), 0);
    }
}

void actualizar_estado_invitacion(MYSQL *conn, int invitacion_id, const char* estado) {
    char query[512];
    
    // Obtener detalles de la invitación
    snprintf(query, sizeof(query),
            "SELECT id_invitador, id_invitado FROM Invitaciones WHERE id_invitacion = %d",
            invitacion_id);
    
    if(mysql_query(conn, query)) return;
    
    MYSQL_RES *res = mysql_store_result(conn);
    if(res) {
        MYSQL_ROW row = mysql_fetch_row(res);
        if(row) {
            int inviador_id = atoi(row[0]);
            int invitado_id = atoi(row[1]);
            
            // Actualizar estado
            snprintf(query, sizeof(query),
                    "UPDATE Invitaciones SET estado = '%s' WHERE id_invitacion = %d",
                    estado, invitacion_id);
            mysql_query(conn, query);
            
            // Notificar a ambos jugadores
            char mensaje[100];
            snprintf(mensaje, sizeof(mensaje), "INV_ACTUALIZADA:%d:%s", invitacion_id, estado);
            
            int socket_inviador = buscar_socket_por_id(inviador_id);
            int socket_invitado = buscar_socket_por_id(invitado_id);
            
            if(socket_inviador != -1) send(socket_inviador, mensaje, strlen(mensaje), 0);
            if(socket_invitado != -1) send(socket_invitado, mensaje, strlen(mensaje), 0);
        }
        mysql_free_result(res);
    }
}
