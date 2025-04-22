#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <unistd.h>

#define PORT 8080
#define MAX_BUFFER 1024

// Genera el string "cards/X/X/X/X" con números aleatorios (1-4)
char* generar_cartas_aleatorias() {
    int cantidades[4];
    char* respuesta = malloc(50); // Suficiente para "cards/X/X/X/X"
    
    srand(time(NULL)); // Inicializar semilla aleatoria

    // Generar 4 números aleatorios entre 1 y 4
    for (int i = 0; i < 4; i++) {
        cantidades[i] = (rand() % 4) + 1;
    }

    // Construir el string "cards/X/X/X/X"
    snprintf(respuesta, 50, "cards/%d/%d/%d/%d", 
             cantidades[0], cantidades[1], cantidades[2], cantidades[3]);

    return respuesta;
}

// Maneja la comunicación con el cliente
void manejar_cliente(int client_socket) {
    char buffer[MAX_BUFFER];
    int bytes_recibidos;

    // Recibir comando del cliente
    bytes_recibidos = recv(client_socket, buffer, MAX_BUFFER, 0);
    if (bytes_recibidos < 0) {
        perror("Error al recibir datos");
        return;
    }
    buffer[bytes_recibidos] = '\0'; // Asegurar terminación nula

    // Si el cliente pide cartas ("GET_CARDS")
    if (strcmp(buffer, "GET_CARDS") == 0) {
        char* cartas_str = generar_cartas_aleatorias();
        printf("Enviando cartas: %s\n", cartas_str);
        send(client_socket, cartas_str, strlen(cartas_str), 0);
        free(cartas_str); // Liberar memoria
    } else {
        const char* error_msg = "Comando no válido. Use 'GET_CARDS'";
        send(client_socket, error_msg, strlen(error_msg), 0);
    }
}

int main() {
    int server_socket, client_socket;
    struct sockaddr_in server_addr, client_addr;
    socklen_t addr_size = sizeof(client_addr);

    // Crear socket
    server_socket = socket(AF_INET, SOCK_STREAM, 0);
    if (server_socket < 0) {
        perror("Error al crear socket");
        exit(1);
    }

    // Configurar dirección del servidor
    server_addr.sin_family = AF_INET;
    server_addr.sin_port = htons(PORT);
    server_addr.sin_addr.s_addr = INADDR_ANY;

    // Enlazar socket
    if (bind(server_socket, (struct sockaddr*)&server_addr, sizeof(server_addr)) < 0) {
        perror("Error al enlazar socket");
        exit(1);
    }

    // Escuchar conexiones
    listen(server_socket, 5);
    printf("Servidor iniciado en el puerto %d. Esperando conexiones...\n", PORT);

    // Bucle principal: aceptar y manejar clientes
    while (1) {
        client_socket = accept(server_socket, (struct sockaddr*)&client_addr, &addr_size);
        if (client_socket < 0) {
            perror("Error al aceptar conexión");
            continue;
        }
        printf("Cliente conectado.\n");

        manejar_cliente(client_socket);
        close(client_socket);
    }

    return 0;
}