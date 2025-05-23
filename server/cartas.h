#ifndef CARTAS_H
#define CARTAS_H

#include <stdbool.h>

typedef enum
{
    CARD_AS,
    CARD_REINA,
    CARD_REY,
    CARD_JOKER
} TipoCarta;

typedef struct
{
    TipoCarta tipo;
    int valor;
} Carta;

#endif