#include <mysql.h>
#include <string.h>
#include <stdlib.h>
#include <stdio.h>
#include "basedatos.h"

int desconectar_base_datos(MYSQL *conn) {
    mysql_close(conn);
    return 0;
}

int usuarioExiste(MYSQL *conn, const char* nombre_usuario) {
    MYSQL_RES *res;
    MYSQL_ROW row;
    char consulta[256];

    snprintf(consulta, sizeof(consulta), "SELECT nombre_usuario FROM Jugadores WHERE nombre_usuario = '%s'", nombre_usuario);

    int err = mysql_query(conn, consulta);
    if (err != 0) {
        printf("Error al consultar datos de la base %u %s\n", mysql_errno(conn), mysql_error(conn));
        exit(1);
    }

    res = mysql_store_result(conn);
    if (res) {
        row = mysql_fetch_row(res);
        mysql_free_result(res);

        if (row) {
            return 1;
        }
    }

    return 0;
}

int insertarUsuario(MYSQL *conn, const char* nombre_usuario, const char* contrasena) {
    char consulta[256];

    snprintf(consulta, sizeof(consulta), "INSERT INTO Jugadores (nombre_usuario, contrasena) VALUES ('%s', '%s')", nombre_usuario, contrasena);

    int err = mysql_query(conn, consulta);
    if (err != 0) {
        printf("Error al introducir datos en la base %u %s\n", mysql_errno(conn), mysql_error(conn));
        exit(1);
    }

    return 0;
}

int verificarCredenciales(MYSQL *conn, const char* nombre_usuario, const char* contrasena) {
    MYSQL_RES *res;
    MYSQL_ROW row;
    char consulta[256];

    snprintf(consulta, sizeof(consulta), "SELECT contrasena FROM Jugadores WHERE nombre_usuario = '%s'", nombre_usuario);

    int err = mysql_query(conn, consulta);
    if (err != 0) {
        printf("Error al consultar datos de la base %u %s\n", mysql_errno(conn), mysql_error(conn));
        exit(1);
    }

    res = mysql_store_result(conn);
    if (res) {
        row = mysql_fetch_row(res);
        mysql_free_result(res);

        if (row) {
            if (strcmp(row[0], contrasena) == 0) {
                return 0;
            }
        }
    }

    return 1;
}

void listarJugadores(MYSQL *conn) {
    MYSQL_RES *res;
    MYSQL_ROW row;

    int err = mysql_query(conn, "SELECT * FROM Jugadores");
    if (err != 0) {
        printf("Error al consultar: %s\n", mysql_error(conn));
        return;
    }

    res = mysql_use_result(conn);
    if (res) {
        printf("Todos los jugadores:\n");
        while ((row = mysql_fetch_row(res))) {
            printf("%s %s %s\n", row[0], row[1], row[2]);
        }
        mysql_free_result(res);
    }
}

void listarPartidas(MYSQL *conn) {
    MYSQL_RES *res;
    MYSQL_ROW row;

    int err = mysql_query(conn, "SELECT * FROM Partidas");
    if (err != 0) {
        printf("Error al consultar: %s\n", mysql_error(conn));
        return;
    }

    res = mysql_use_result(conn);
    if (res) {
        printf("\nTodas las partidas:\n");
        while ((row = mysql_fetch_row(res))) {
            printf("%s %s %s %s %s\n", row[0], row[1], row[2], row[3], row[4]);
        }
        mysql_free_result(res);
    }
}

void listarPartidasGanadas(MYSQL *conn, const char* nombre_usuario) {
    MYSQL_RES *res;
    MYSQL_ROW row;
    char consulta[256];

    snprintf(consulta, sizeof(consulta), "SELECT id_jugador FROM Jugadores WHERE nombre_usuario = '%s'", nombre_usuario);
    int err = mysql_query(conn, consulta);
    if (err != 0) {
        printf("Error al consultar: %s\n", mysql_error(conn));
        return;
    }

    res = mysql_store_result(conn);
    if (res) {
        row = mysql_fetch_row(res);
        if (row) {
            snprintf(consulta, sizeof(consulta), "SELECT * FROM Partidas WHERE ganador_id = '%s'", row[0]);
            err = mysql_query(conn, consulta);
            if (err != 0) {
                printf("Error al consultar: %s\n", mysql_error(conn));
                mysql_free_result(res);
                return;
            }

            MYSQL_RES *res2 = mysql_store_result(conn);
            if (res2) {
                printf("\nPartidas ganadas por %s:\n", nombre_usuario);
                while ((row = mysql_fetch_row(res2))) {
                    printf("%s %s %s %s %s\n", row[0], row[1], row[2], row[3], row[4]);
                }
                mysql_free_result(res2);
            }
        }
        mysql_free_result(res);
    }
}