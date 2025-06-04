#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <pthread.h>
#include "generarcartas.h"

// Definición del mazo completo
#define TAMANIO_MAZO 20
#define CARTAS_AS 6
#define CARTAS_KING 6
#define CARTAS_QUEEN 6
#define CARTAS_JOKER 2

// Estructura para representar el mazo
typedef struct
{
    int cartas[TAMANIO_MAZO]; // 1=as, 2=king, 3=queen, 4=joker
    int posicion_actual;      // Posición en el mazo
} Mazo;

// Mazo global
static Mazo mazo;
static pthread_mutex_t mazo_mutex = PTHREAD_MUTEX_INITIALIZER;
static int mazo_inicializado = 0;

// Función para inicializar el mazo
void inicializar_mazo()
{
    int indice = 0;

    // Añadir ases
    for (int i = 0; i < CARTAS_AS; i++)
    {
        mazo.cartas[indice++] = 1;
    }

    // Añadir reyes
    for (int i = 0; i < CARTAS_KING; i++)
    {
        mazo.cartas[indice++] = 2;
    }

    // Añadir reinas
    for (int i = 0; i < CARTAS_QUEEN; i++)
    {
        mazo.cartas[indice++] = 3;
    }

    // Añadir jokers
    for (int i = 0; i < CARTAS_JOKER; i++)
    {
        mazo.cartas[indice++] = 4;
    }

    mazo.posicion_actual = 0;
}

// Función para mezclar el mazo (algoritmo Fisher-Yates)
void mezclar_mazo()
{
    srand(time(NULL));

    for (int i = TAMANIO_MAZO - 1; i > 0; i--)
    {
        int j = rand() % (i + 1);
        // Intercambiar cartas[i] con cartas[j]
        int temp = mazo.cartas[i];
        mazo.cartas[i] = mazo.cartas[j];
        mazo.cartas[j] = temp;
    }
}

// Función para obtener cartas del mazo
char *generar_cartas_aleatorias()
{
    pthread_mutex_lock(&mazo_mutex);

    // Inicializar el mazo si es necesario
    if (!mazo_inicializado)
    {
        inicializar_mazo();
        mezclar_mazo();
        mazo_inicializado = 1;
    }

    // Reiniciar y mezclar el mazo si se acaban las cartas
    if (mazo.posicion_actual >= TAMANIO_MAZO - 4)
    { // Necesitamos al menos 4 cartas
        printf("Reiniciando y mezclando el mazo...\n");
        inicializar_mazo();
        mezclar_mazo();
    }

    // Extraer 4 cartas del mazo
    int carta1 = mazo.cartas[mazo.posicion_actual++];
    int carta2 = mazo.cartas[mazo.posicion_actual++];
    int carta3 = mazo.cartas[mazo.posicion_actual++];
    int carta4 = mazo.cartas[mazo.posicion_actual++];

    // Contar cuántas hay de cada tipo para el formato de respuesta
    int cantidades[5] = {0}; // Índice 0 no se usa, 1=as, 2=king, 3=queen, 4=joker
    cantidades[carta1]++;
    cantidades[carta2]++;
    cantidades[carta3]++;
    cantidades[carta4]++;

    char *respuesta = malloc(50); // Suficiente para "CARDS/X/X/X/X"

    // Construir el string "CARDS/X/X/X/X"
    snprintf(respuesta, 50, "CARDS/%d/%d/%d/%d",
             cantidades[1], cantidades[2], cantidades[3], cantidades[4]);

    printf("He generado cartas para el cliente: %s\n", respuesta);
    printf("Quedan %d cartas en el mazo\n", TAMANIO_MAZO - mazo.posicion_actual);

    pthread_mutex_unlock(&mazo_mutex);
    return respuesta;
}