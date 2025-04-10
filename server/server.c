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
#include <errno.h>
#include "basedatos.h"
#include "auth.h"

// Estructura para mantener los clientes conectados
typedef struct ClientNode {
    int socket;
    char usuario[50];
    struct ClientNode* next;
} ClientNode;

ClientNode* client_list = NULL;
pthread_mutex_t client_list_mutex = PTHREAD_MUTEX_INITIALIZER;

// Función para añadir un cliente a la lista
void add_client(int sock, const char* usuario) {
    pthread_mutex_lock(&client_list_mutex);
    
    ClientNode* new_node = (ClientNode*)malloc(sizeof(ClientNode));
    new_node->socket = sock;
    strncpy(new_node->usuario, usuario, sizeof(new_node->usuario)-1);
    new_node->usuario[sizeof(new_node->usuario)-1] = '\0';
    new_node->next = client_list;
    client_list = new_node;
    
    pthread_mutex_unlock(&client_list_mutex);
}

// Función para eliminar un cliente de la lista
void remove_client(int sock) {
    pthread_mutex_lock(&client_list_mutex);
    
    ClientNode** pp = &client_list;
    while (*pp) {
        if ((*pp)->socket == sock) {
            ClientNode* to_free = *pp;
            *pp = (*pp)->next;
            free(to_free);
            break;
        }
        pp = &(*pp)->next;
    }
    
    pthread_mutex_unlock(&client_list_mutex);
}

// Función para enviar datos a todos los clientes
void broadcast_to_all(const char* message) {
    pthread_mutex_lock(&client_list_mutex);
    
    ClientNode* current = client_list;
    while (current != NULL) {
        if (write(current->socket, message, strlen(message)) < 0) {
            perror("Error al enviar broadcast");
            // Si hay error, probablemente el cliente se desconectó
            ClientNode* to_remove = current;
            current = current->next;
            remove_client(to_remove->socket);
            continue;
        }
        current = current->next;
    }
    
    pthread_mutex_unlock(&client_list_mutex);
}

void* cliente(void* socket_ptr) {
    int sock_conn = *((int*)socket_ptr);
    free(socket_ptr); // Liberamos la memoria ahora que tenemos el valor
    char peticion[512];
    char respuesta[1024];
    int ret;
    char usuario[50] = {0}; // Para mantener el nombre de usuario

    MYSQL *conn = mysql_init(NULL);
    if (!mysql_real_connect(conn, "shiva2.upc.es", "root", "mysql", "duska_project", 0, NULL, 0)) {
        printf("Error MySQL: %s\n", mysql_error(conn));
        strcpy(respuesta, "Error en la base de datos");
        write(sock_conn, respuesta, strlen(respuesta));
        close(sock_conn);
        return NULL;
    }

    while (1) {
        ret = read(sock_conn, peticion, sizeof(peticion)-1);
        if (ret <= 0) {
            // Cliente desconectado
            if (strlen(usuario) > 0) {
                // Actualizar estado como desconectado
                actualizarEstado(conn, usuario, 0);
                
                // Enviar lista actualizada a todos
                char buffer[1024] = {0};
                listarConectados(conn, buffer, sizeof(buffer));
                broadcast_to_all(buffer);
                
                // Eliminar de la lista de clientes
                remove_client(sock_conn);
            }
            break;
        }
        peticion[ret] = '\0';
        printf("Peticion: %s\n", peticion);

        char *p = strtok(peticion, "/");
        int codigo = atoi(p);
        p = strtok(NULL, "/");
        if (p != NULL) {
            strncpy(usuario, p, sizeof(usuario)-1);
            usuario[sizeof(usuario)-1] = '\0';
        }
        p = strtok(NULL, "/");
        char contrasena[72] = {0};
        if (p != NULL) {
            strncpy(contrasena, p, sizeof(contrasena)-1);
            contrasena[sizeof(contrasena)-1] = '\0';
        }

        memset(respuesta, 0, sizeof(respuesta));

        if (codigo == 0) {
            int reg_result = registrarUsuario(conn, usuario, contrasena);
            snprintf(respuesta, sizeof(respuesta), "%d", reg_result);
        } 
        else if (codigo == 1) {
            int login_result = iniciarSesion(conn, usuario, contrasena);
            if (login_result == 0) {
                add_client(sock_conn, usuario); // Añadir a la lista de clientes
            }
            snprintf(respuesta, sizeof(respuesta), "%d", login_result);
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
            int estado = atoi(contrasena);
            int update_result = actualizarEstado(conn, usuario, estado);
            
            if (update_result == 0) {
                char buffer[1024] = {0};
                listarConectados(conn, buffer, sizeof(buffer));
                strcpy(respuesta, buffer);
                
                // Enviar la lista actualizada a todos los clientes
                broadcast_to_all(buffer);
            } else {
                strcpy(respuesta, "Error al actualizar estado");
            }
        }

        printf("Resultado: %s\n", respuesta);
        if (write(sock_conn, respuesta, strlen(respuesta)) < 0) {
            perror("Error al escribir en socket");
            break;
        }
    }

    mysql_close(conn);
    close(sock_conn);
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