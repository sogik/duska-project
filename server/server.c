#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <ctype.h>
#include <mysql.h>
#include <sys/types.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <unistd.h>
#include <pthread.h>
#include "basedatos.h"
#include "auth.h"

#define MAX_CLIENTES 100

int clientes[MAX_CLIENTES];
int num_clientes = 0;
pthread_mutex_t mutex_clientes = PTHREAD_MUTEX_INITIALIZER;

void enviarATodos(const char* mensaje) {
    pthread_mutex_lock(&mutex_clientes);
    for (int i = 0; i < num_clientes; i++) {
        if (clientes[i] != -1) {
            int r = write(clientes[i], mensaje, strlen(mensaje));
            if (r < 0) {
                perror("Error enviando a cliente");
            }
        }
    }
    pthread_mutex_unlock(&mutex_clientes);
}

void* cliente(void* socket_ptr) {
    int sock_conn = *((int*)socket_ptr);
    char peticion[512];
    char respuesta[1024];
    int ret;

    // Leer hasta '\n'
    int pos = 0;
    char c;
    while (read(sock_conn, &c, 1) > 0 && c != '\n' && pos < sizeof(peticion) - 1) {
        peticion[pos++] = c;
    }
    peticion[pos] = '\0';

    printf("Peticion: %s\n", peticion);

    MYSQL *conn = mysql_init(NULL);
    if (!mysql_real_connect(conn, "localhost", "duska_user", "tu_contraseña", "duska_project", 0, NULL, 0)) {
        printf("Error MySQL: %s\n", mysql_error(conn));
        strcpy(respuesta, "Error en la base de datos");
        write(sock_conn, respuesta, strlen(respuesta));
        close(sock_conn);
        free(socket_ptr);
        return NULL;
    }

    // Parseo seguro
    char *p = strtok(peticion, "/");
    int codigo = atoi(p ? p : "0");

    p = strtok(NULL, "/");
    char usuario[50] = "";
    if (p) strcpy(usuario, p);

    p = strtok(NULL, "/");
    char contrasena[72] = "";
    if (p) strcpy(contrasena, p);

    if (codigo == 0) {
        int res = registrarUsuario(conn, usuario, contrasena);
        sprintf(respuesta, "%d", res);
    } 
    else if (codigo == 1) {
        int res = iniciarSesion(conn, usuario, contrasena);
        sprintf(respuesta, "%d", res);
    } 
    else if (codigo == 2) {
        listarJugadores(conn, respuesta, sizeof(respuesta));
    } 
    else if (codigo == 3) {
        listarPartidas(conn, respuesta, sizeof(respuesta));
    } 
    else if (codigo == 4) {
        listarPartidasGanadas(conn, usuario, respuesta, sizeof(respuesta));
    }
    else if (codigo == 5) {
        listarConectados(conn, respuesta, sizeof(respuesta));
    }
    else if (codigo == 6) {
        char buffer[1024] = {0};
        int estado = actualizarEstado(conn, usuario, atoi(contrasena));
        printf("Estado: %d\n", estado);

        if (estado == 0) {
            listarConectados(conn, buffer, sizeof(buffer));
            strcpy(respuesta, buffer);
            printf("Buffer conectados: %s\n", buffer);

            // Enviar a todos los clientes conectados
            enviarAClientes(buffer);
        } else {
            strcpy(respuesta, "Error al cambiar estado");
        }
    }

    printf("Resultado: %s\n", respuesta);
    write(sock_conn, respuesta, strlen(respuesta));

    mysql_close(conn);
    close(sock_conn);
    free(socket_ptr);

    return NULL;
}

int main(int argc, char *argv[]) {
    int sock_conn, sock_listen;
    struct sockaddr_in serv_adr;

    if ((sock_listen = socket(AF_INET, SOCK_STREAM, 0)) < 0) {
        perror("Error creant socket");
        exit(1);
    }

    memset(&serv_adr, 0, sizeof(serv_adr));
    serv_adr.sin_family = AF_INET;
    serv_adr.sin_addr.s_addr = htonl(INADDR_ANY);
    serv_adr.sin_port = htons(50756);

    if (bind(sock_listen, (struct sockaddr *) &serv_adr, sizeof(serv_adr)) < 0) {
        perror("Error al bind");
        close(sock_listen);
        exit(1);
    }

    if (listen(sock_listen, 10) < 0) {
        perror("Error en el listen");
        close(sock_listen);
        exit(1);
    }

    printf("Servidor escuchando en el puerto 50756...\n");

    while (1) {
        sock_conn = accept(sock_listen, NULL, NULL);
        if (sock_conn < 0) {
            perror("Error en accept");
            continue;
        }
        printf("Nuevo cliente conectado.\n");

        int* socket_ptr = malloc(sizeof(int));
        *socket_ptr = sock_conn;

        pthread_t hilo;
        if (pthread_create(&hilo, NULL, cliente, socket_ptr) != 0) {
            perror("Error al crear hilo");
            close(sock_conn);
            free(socket_ptr);
        } else {
            pthread_detach(hilo);
        }
    }

    close(sock_listen);
    return 0;
}