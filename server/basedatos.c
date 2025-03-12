#include <mysql.h>
#include <string.h>
#include <stdlib.h>
#include <stdio.h>
#include "basedatos.h"

int desconectar_base_datos(MYSQL *conn)
{
    mysql_close (conn);
    exit(0);
}

int usuarioExiste(MYSQL *conn, const char* nombre_usuario) {

    MYSQL_RES *res;
    MYSQL_ROW row;

    char consulta[256];

    int err=mysql_query (conn, "SELECT nombre_usuario FROM Jugadores");

    if (err!=0) 
    {
        printf ("Error al consultar datos de la base %u %s\n",
        mysql_errno(conn), mysql_error(conn));
        exit (1);
    }

    res = mysql_store_result (conn);
    row = mysql_fetch_row (res);

    if (row == NULL)
    {
        printf ("No se han obtenido datos en la consulta\n");
        exit (1);
    }  
    else
        while (row !=NULL) 
        {
            printf ("Usuario: %s\n", row[0]);
            row = mysql_fetch_row (res);
        }
        
    strcpy (consulta,"SELECT nombre_usuario FROM Jugadores WHERE nombre_usuario = '");
    strcat (consulta, nombre_usuario);
    strcat (consulta,"'");

    err=mysql_query (conn, consulta);

    if (err!=0) 
    {
        printf ("Error al consultar datos de la base %u %s\n",
        mysql_errno(conn), mysql_error(conn));
        exit (1);
    }
    else
    {
        res = mysql_store_result (conn);
        row = mysql_fetch_row (res);
        mysql_free_result(res);

        if (row == NULL)
        {
            return 1;
        }
    }

    return 0;
}

int insertarUsuario(MYSQL *conn, const char* nombre_usuario, const char* contrasena) {

    MYSQL_RES *res;
    MYSQL_ROW row;

    char consulta[256];

    strcpy (consulta,"INSERT INTO Jugadores VALUES ('");
    strcat (consulta, nombre_usuario);
    strcat (consulta,"','");

    strcat (consulta, contrasena);
    strcat (consulta, ");");

    int err=mysql_query (conn, consulta);

    if (err!=0) 
    {
        printf ("Error al  introducir datos la base %u %s\n",
        mysql_errno(conn), mysql_error(conn));
        exit (1);
    }

    return 0;
    mysql_free_result(res);
}

int verificarCredenciales(MYSQL *conn, const char* nombre_usuario, const char* contrasena) {

    MYSQL_RES *res;
    MYSQL_ROW row;

    char consulta[256];

    int err=mysql_query (conn, "SELECT nombre_usuario, contraseña FROM Jugadores");

    if (err!=0) 
    {
        printf ("Error al consultar datos de la base %u %s\n",
        mysql_errno(conn), mysql_error(conn));
        exit (1);
    }

    res = mysql_store_result (conn);
    row = mysql_fetch_row (res);
    mysql_free_result(res);

    if (row == NULL)
    {
        printf ("No se han obtenido datos en la consulta\n");
        exit (1);
    }  
    else
        while (row !=NULL) 
        {
            row = mysql_fetch_row (res);
        }
        
    strcpy (consulta,"SELECT constraseña FROM Jugadores WHERE nombre_usuario = '");
    strcat (consulta, nombre_usuario);
    strcat (consulta,"'");

    err=mysql_query (conn, consulta);

    if (err!=0) 
    {
        printf ("Error al consultar datos de la base %u %s\n",
        mysql_errno(conn), mysql_error(conn));
        exit (1);
    }
    else
    {
        res = mysql_store_result (conn);
        row = mysql_fetch_row (res);
        mysql_free_result(res);

        if (row == NULL)
        {
            return 1;
        }
        else
        {
            if (strcmp(row[0], contrasena) == 0)
            {
                return 0;
            }
        }
    }

    return 3;
}

void listarJugadores(MYSQL *conn) {

    MYSQL_RES *res;
    MYSQL_ROW row;

    char consulta[256];

    // Mostrar todos los jugadores
    strcpy(consulta, "SELECT * FROM Jugadores");
    int err = mysql_query(conn, consulta);
    if (err != 0) {
        printf("Error al consultar\n");
        mysql_close(conn);
        exit(1);
    }

    res = mysql_use_result(conn);
    printf("Todos los jugadores:\n");
    while ((row = mysql_fetch_row(res))) {
        printf("%s %s %s\n", row[0], row[1], row[2]);
    }
    mysql_free_result(res);
}

void listarPartidas(MYSQL *conn) {

    MYSQL_RES *res;
    MYSQL_ROW row;

    char consulta[256];

    // Mostrar todas las partidas
    strcpy(consulta, "SELECT * FROM Partidas");
    int err = mysql_query(conn, consulta);
    if (err != 0) {
        printf("Error al consultar\n");
        mysql_close(conn);
        exit(1);
    }

    res = mysql_use_result(conn);
    printf("\nTodas las partidas:\n");
    while ((row = mysql_fetch_row(res))) {
        printf("%s %s %s %s %s\n", row[0], row[1], row[2], row[3], row[4]);
    }
    mysql_free_result(res);
}


// EN PRUEBA
void listarPartidasGanadas(MYSQL *conn, const char* nombre_usuario) {
    
    MYSQL *conn;
    MYSQL_RES *res;
    MYSQL_ROW row;

    char consulta[256];

    strcpy(consulta, "SELECT id_jugador FROM Jugadores WHERE nombre_usuario = '");
    strcat(consulta, nombre_usuario);
    int err = mysql_query(conn, consulta);
    if (err != 0) {
        printf("Error al consultar\n");
        mysql_close(conn);
        exit(1);
    }
    res = mysql_use_result(conn);
    row = mysql_fetch_row(res);
    if (row == NULL) {
        printf("No se han obtenido datos en la consulta\n");
        exit(1);
    }

    // Mostrar partidas ganadas por un jugador
    strcpy(consulta, "SELECT * FROM Partidas WHERE ganador_id = '");
    strcat(consulta, row[0]);
    err = mysql_query(conn, consulta);
    if (err != 0) {
        printf("Error al consultar\n");
        mysql_close(conn);
        exit(1);
    }

    res = mysql_use_result(conn);
    printf("\nPartidas ganadas por cada jugador:\n");
    while ((row = mysql_fetch_row(res))) {
        printf("%s: %s partidas\n", row[0], row[1]);
    }
    mysql_free_result(res);
}