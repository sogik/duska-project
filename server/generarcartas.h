#ifndef GENERARCARTAS_H
#define GENERARCARTAS_H

// Definición del mazo completo
#define TAMANIO_MAZO 20
#define CARTAS_AS 6
#define CARTAS_KING 6
#define CARTAS_QUEEN 6
#define CARTAS_JOKER 2

// Función principal para generar 4 cartas aleatorias
// Devuelve un string con formato "CARDS/n_as/n_king/n_queen/n_joker"
// NOTA: El llamador es responsable de liberar la memoria del string devuelto
char *generar_cartas_aleatorias();

#endif /* GENERARCARTAS_H */