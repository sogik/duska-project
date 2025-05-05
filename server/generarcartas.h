/*#ifndef GENERARCARTAS_H
#define GENERARCARTAS_H

#include <mysql.h>
#include <pthread.h>

// Funciones
char* generar_cartas_aleatorias();

#endif*/

#ifndef GENERARCARTAS_H
#define GENERARCARTAS_H

// Estructura para representar el estado del mazo
typedef struct {
    int cartas[20];   // 1=AS, 2=REY, 3=REINA, 4=JOKER
    int cartas_disponibles;
} Mazo;

// Inicializar el mazo para una nueva ronda
Mazo* inicializar_mazo();

// Liberar la memoria del mazo
void liberar_mazo(Mazo* mazo);

// Generar cartas para un cliente
char* generar_cartas_aleatorias(Mazo* mazo, int num_cartas);

#endif