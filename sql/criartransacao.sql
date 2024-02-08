CREATE OR REPLACE FUNCTION criartransacao(
  IN idcliente integer,
  IN valor integer,
  IN descricao varchar(10)
) RETURNS TABLE(result integer, saldo2 integer, limite2 integer) AS $$
DECLARE
  clienteencontrado cliente%rowtype;
BEGIN
  SELECT * FROM cliente
  INTO clienteencontrado
  WHERE id = idcliente;

  IF not found THEN
    --raise notice'Id do Cliente % não encontrado.', idcliente;
    RETURN QUERY select -1, 0, 0;
    RETURN;
  END IF;

  IF clienteencontrado.saldo + valor < clienteencontrado.limite THEN
    --raise notice'Cliente % excedeu o limite.', idcliente;
    RETURN QUERY select -2, 0, 0;
    RETURN;
  END IF;

  --raise notice'Criando transacao para cliente %.', idcliente;
  INSERT INTO transacao (valor, descricao, realizadaem, idcliente)
    VALUES (valor, descricao, now() at time zone 'utc', idcliente);
  UPDATE cliente
    SET saldo = saldo + valor
    WHERE id = idcliente;
  RETURN QUERY
    SELECT 0, saldo, limite
      FROM cliente
      WHERE id = idcliente;
END;$$ LANGUAGE plpgsql;

