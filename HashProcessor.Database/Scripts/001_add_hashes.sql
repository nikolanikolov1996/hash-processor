CREATE TABLE IF NOT EXISTS hashes
(
    id BIGINT NOT NULL AUTO_INCREMENT,
    `date` DATETIME(6) NOT NULL,
    sha1 CHAR(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,

    CONSTRAINT pk_hashes PRIMARY KEY (id),
    CONSTRAINT ux_hashes_sha1 UNIQUE (sha1),
    INDEX ix_hashes_date (`date`)
) ENGINE = InnoDB;