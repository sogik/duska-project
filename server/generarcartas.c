#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include "generarcartas.h"

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
    snprintf(respuesta, 50, "CARDS/%d/%d/%d/%d", 
             cantidades[0], cantidades[1], cantidades[2], cantidades[3]);

    printf("He generado cartas para el cliente:%s", respuesta);

    return respuesta;
}