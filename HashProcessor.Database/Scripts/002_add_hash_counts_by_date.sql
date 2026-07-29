CREATE TABLE IF NOT EXISTS hash_counts_by_date
(
    `date` DATE NOT NULL,
    `count` BIGINT NOT NULL,

    CONSTRAINT pk_hash_counts_by_date PRIMARY KEY (`date`)
) ENGINE = InnoDB;

INSERT INTO hash_counts_by_date (`date`, `count`)
SELECT DATE(`date`), COUNT(*)
FROM hashes
GROUP BY DATE(`date`)
ON DUPLICATE KEY UPDATE `count` = VALUES(`count`);
