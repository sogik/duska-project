#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <ctype.h>
#include <mysql.h>
#include <sys/types.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <unistd.h>
#include "basedatos.h"
#include "auth.h"

int main(int argc, char *argv[])
{
    int sock_conn, sock_listen, ret;
    struct sockaddr_in serv_adr;
    char peticion[512];
    char respuesta[512];

    if ((sock_listen = socket(AF_INET, SOCK_STREAM, 0)) < 0) {
        perror("Error creant socket");
        exit(1);
    }

    memset(&serv_adr, 0, sizeof(serv_adr));
    serv_adr.sin_family = AF_INET;
    serv_adr.sin_addr.s_addr = htonl(INADDR_ANY);
    serv_adr.sin_port = htons(9050);

    if (bind(sock_listen, (struct sockaddr *) &serv_adr, sizeof(serv_adr)) < 0) {
        perror("Error al bind");
        close(sock_listen);
        exit(1);
    }

    if (listen(sock_listen, 3) < 0) {
        perror("Error en el listen");
        close(sock_listen);
        exit(1);
    }

    printf("Eschuchando\n");

    while (1) {
        sock_conn = accept(sock_listen, NULL, NULL);
        printf("He recibido conexion\n");

        ret = read(sock_conn, peticion, sizeof(peticion));
        printf("Recibido\n");

        peticion[ret] = '\0';
        printf("Peticion: %s\n", peticion);

        // Conexion a la base de datos
        MYSQL *conn;
        MYSQL_RES *res;
        MYSQL_ROW row;

        conn = mysql_init(NULL);
        if (conn == NULL) {
            printf("Error al crear la conexion: %u %s\n", mysql_errno(conn), mysql_error(conn));
            exit(1);
        }

        conn = mysql_real_connect(conn, "localhost", "root", "mysql", "duska_project", 0, NULL, 0);
        if (conn == NULL) {
            printf("Error al inicializar la conexion: %u %s\n", mysql_errno(conn), mysql_error(conn));
            exit(1);
        }

        // Procesar la petición
        char *p = strtok(peticion, "/");
        int codigo = atoi(p);
        p = strtok(NULL, "/");
        char usuario[50];
        strcpy(usuario, p);
        p = strtok(NULL, "/");
        char contrasena[72];
        if (p != NULL) {
            strcpy(contrasena, p);
        } else {
            strcpy(contrasena, "");
        }

        if (codigo == 0) {
            if (registrarUsuario(conn, usuario, contrasena) == 0) 
            {
                strcpy(respuesta, "Usuario registrado correctamente");
            } else if (registrarUsuario(conn, usuario, contrasena) == 1) {
                strcpy(respuesta, "El nombre de usuario ya está en uso"); 
            }
            else {
                strcpy(respuesta, "Error al registrar usuario");
            }
        } else if (codigo == 1) {
            if (iniciarSesion(conn, usuario, contrasena) == 0) 
            {
                strcpy(respuesta, "Sesion iniciada correctamente");
            } else if (iniciarSesion(conn, usuario, contrasena) == 1) 
            {
                strcpy(respuesta, "Contraseña incorrecta");
            } else {
                strcpy(respuesta, "El nombre de usuario no existe");
            }
        } else if (codigo == 2) {
            strcpy(respuesta, listarJugadores(conn));
        } else if (codigo == 3) {
            strcpy(respuesta, listarPartidas(conn));
        } else {
            strcpy(respuesta, listarJugadores(conn));
        }

        printf("Resultado: %s\n", respuesta);
        write(sock_conn, respuesta, strlen(respuesta));

        desconectar_base_datos(conn);
        close(sock_conn);
    }

    close(sock_listen);
    return 0;
}