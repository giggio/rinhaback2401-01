CREATE OR REPLACE FUNCTION criartransacao(
  IN idcliente integer,
  IN valor integer,
  IN descricao varchar(10)
) RETURNS RECORD AS $$
DECLARE
  clienteencontrado cliente%rowtype;
  ret RECORD;
BEGIN
  SELECT * FROM cliente
  INTO clienteencontrado
  WHERE id = idcliente;

  IF not found THEN
    --raise notice'Id do Cliente % não encontrado.', idcliente;
    select -1 into ret;
    RETURN ret;
  END IF;

  IF clienteencontrado.saldo + valor < clienteencontrado.limite THEN
    --raise notice'Cliente % excedeu o limite.', idcliente;
    select -2 into ret;
    RETURN ret;
  END IF;

  --raise notice'Criando transacao para cliente %.', idcliente;
  INSERT INTO transacao (valor, descricao, realizadaem, idcliente)
    VALUES (valor, descricao, now() at time zone 'utc', idcliente);
  UPDATE cliente
    SET saldo = saldo + valor
    WHERE id = idcliente;
  SELECT saldo, limite
    FROM cliente
    WHERE id = idcliente
    INTO ret;
  RETURN ret;
END;$$ LANGUAGE plpgsql;
