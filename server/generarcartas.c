/*#include <stdio.h>
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
}*/

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include "generarcartas.h"

Mazo* inicializar_mazo() {
    Mazo* mazo = malloc(sizeof(Mazo));
    int i = 0;
    
    // 6 ases
    for (int j = 0; j < 6; j++) {
        mazo->cartas[i++] = 1;
    }
    
    // 6 reyes
    for (int j = 0; j < 6; j++) {
        mazo->cartas[i++] = 2;
    }
    
    // 6 reinas
    for (int j = 0; j < 6; j++) {
        mazo->cartas[i++] = 3;
    }
    
    // 2 jokers
    for (int j = 0; j < 2; j++) {
        mazo->cartas[i++] = 4;
    }
    
    mazo->cartas_disponibles = 20;
    
    // Mezclar el mazo (algoritmo Fisher-Yates)
    srand(time(NULL));
    for (i = 19; i > 0; i--) {
        int j = rand() % (i + 1);
        int temp = mazo->cartas[i];
        mazo->cartas[i] = mazo->cartas[j];
        mazo->cartas[j] = temp;
    }
    
    return mazo;
}

void liberar_mazo(Mazo* mazo) {
    free(mazo);
}

char* generar_cartas_aleatorias(Mazo* mazo, int num_cartas) {
    // Verificar que haya suficientes cartas disponibles
    if (num_cartas > mazo->cartas_disponibles) {
        num_cartas = mazo->cartas_disponibles;
    }
    
    int cantidades[4] = {0, 0, 0, 0}; // Contador para AS, REY, REINA, JOKER
    
    // Tomar cartas del mazo
    for (int i = 0; i < num_cartas; i++) {
        int carta = mazo->cartas[mazo->cartas_disponibles - 1];
        mazo->cartas_disponibles--;
        cantidades[carta - 1]++; // -1 porque los tipos empiezan en 1
    }
    
    char* respuesta = malloc(50); // Suficiente para "CARDS/X/X/X/X"
    
    // Construir el string "CARDS/X/X/X/X"
    snprintf(respuesta, 50, "CARDS/%d/%d/%d/%d", 
             cantidades[0], cantidades[1], cantidades[2], cantidades[3]);
    
    printf("He generado cartas para el cliente: %s\n", respuesta);
    
    return respuesta;
}