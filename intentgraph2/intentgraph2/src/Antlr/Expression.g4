grammar Expression;
prog:   expr EOF ;
expr:   '!' expr
    |   expr ('>'|'<'|'>='|'<='|'=='|'!=') expr
    |   expr '&&' expr
    |   expr '||' expr
    |   BOOL
    |   INT
    |   VAR
    |   '(' expr ')'
    ;

SPACE   : [ \t\r\n]+ -> skip;
BOOL    : 'true' | 'false';
INT     : '-'? [0-9]+ ;
VAR     : ([a-zA-Z_][a-zA-Z_0-9]* '.')?[a-zA-Z_][a-zA-Z_0-9]*;
